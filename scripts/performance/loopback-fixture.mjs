import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { once } from 'node:events';
import { access, cp, mkdtemp, readFile, rm } from 'node:fs/promises';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import { basename, dirname, extname, join, resolve, sep } from 'node:path';

const fixtureHost = '127.0.0.1';
const contentTypes = {
    '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json',
    '.wasm': 'application/wasm', '.css': 'text/css', '.ico': 'image/x-icon'
};

export async function startLoopbackFixture(browserPublish, serverPublish) {
    const site = browserPublish === null ? null : resolve(browserPublish);
    const server = resolve(serverPublish);
    if (site) await access(join(site, 'index.html'));
    await access(join(server, 'Egroo.Server.dll'));

    const tempRoot = resolve(tmpdir());
    const fixture = await mkdtemp(join(tempRoot, 'egroo-conversation-'));
    assert.equal(dirname(fixture), tempRoot);

    let api;
    let backend;
    let backendClosed;
    const frontend = site && createServer(async (request, response) => {
        const { pathname } = new URL(request.url, 'http://' + fixtureHost);
        if (pathname === '/egroo-config.json') {
            response.writeHead(200, { 'Content-Type': 'application/json' });
            response.end(JSON.stringify({ apiUrl: api }));
            return;
        }

        const path = pathname === '/' ? 'index.html' : pathname.slice(1);
        const asset = resolve(site, path);
        try {
            if (!asset.startsWith(site + sep)) {
                response.writeHead(404).end();
                return;
            }

            const content = await readFile(asset);
            response.writeHead(200, { 'Content-Type': contentTypes[extname(asset)] ?? 'application/octet-stream' });
            response.end(content);
        } catch {
            response.writeHead(404).end();
        }
    });

    async function close() {
        if (backend) {
            backend.kill();
            await backendClosed;
        }
        if (frontend?.listening) {
            const closed = once(frontend, 'close');
            frontend.close();
            frontend.closeAllConnections();
            await closed;
        }
        await rm(fixture, { recursive: true, force: true, maxRetries: 3 });
    }

    try {
        const hosted = join(fixture, 'server');
        await cp(server, hosted, {
            recursive: true,
            filter: path => !/^(App_Data|appsettings(?:\..+)?\.json|\.env(?:\..+)?)$/i.test(basename(path))
        });
        let origin;
        if (frontend) {
            frontend.listen(0, fixtureHost);
            await once(frontend, 'listening');
            origin = 'http://' + fixtureHost + ':' + frontend.address().port;
        }

        backend = spawn('dotnet', [join(hosted, 'Egroo.Server.dll')], {
            cwd: fixture,
            windowsHide: true,
            stdio: ['ignore', 'pipe', 'ignore'],
            env: {
                ...process.env,
                ASPNETCORE_URLS: 'http://' + fixtureHost + ':0',
                ASPNETCORE_ENVIRONMENT: 'Production',
                DOTNET_ENVIRONMENT: 'Production',
                Egroo__LocalDebug: 'false',
                Database__Provider: 'SQLite',
                ConnectionStrings__Sqlite: 'Data Source=' + join(fixture, 'egroo.db'),
                Secrets__Jwt: randomBytes(48).toString('hex'),
                Encryption__Key: randomBytes(16).toString('hex'),
                Encryption__IV: randomBytes(8).toString('hex'),
                Api__AllowedOrigins__0: origin ?? 'http://' + fixtureHost,
                Logging__LogLevel__Default: 'Warning',
                'Logging__LogLevel__Microsoft.Hosting.Lifetime': 'Information'
            }
        });
        backendClosed = new Promise(resolve => backend.once('close', resolve));
        api = await waitForApi(backend);
        return { api, origin, close };
    } catch (error) {
        await close();
        throw error;
    }
}

async function waitForApi(backend) {
    const { promise, resolve, reject } = Promise.withResolvers();
    const timer = setTimeout(() => reject(new Error('The disposable Egroo API did not bind within 30 seconds.')), 30_000);
    backend.once('error', reject);
    backend.once('exit', code => reject(new Error('The disposable Egroo API stopped with code ' + code)));
    backend.stdout.setEncoding('utf8');
    let output = '';
    backend.stdout.on('data', chunk => {
        output = (output + chunk).slice(-8192);
        const url = output.match(/Now listening on:\s*(http:\/\/127\.0\.0\.1:\d+)\s/)?.[1];
        if (url) resolve(url);
    });
    try {
        return await promise;
    } finally {
        clearTimeout(timer);
    }
}
