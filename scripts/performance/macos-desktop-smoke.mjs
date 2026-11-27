import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { access, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { constants } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const runtimeProof = `
(() => {
    const runtime = globalThis.egrooRuntime;
    const report = name => fetch('/smoke/' + name, { method: 'POST' });
    const requests = new Map();
    const storage = new Set();
    const { append, start, read, clear } = runtime;
    runtime.append = (id, chunk) => {
        requests.set(id, (requests.get(id) || '') + chunk);
        return append(id, chunk);
    };
    runtime.start = id => {
        const request = JSON.parse(atob(requests.get(id)));
        if (request.method === 'storage.get' && request.args[0].startsWith('egroo.session:'))
            storage.add(id);
        requests.delete(id);
        return start(id);
    };
    runtime.read = (id, offset) => {
        const result = read(id, offset);
        if (result && storage.delete(id) && JSON.parse(result).value === null)
            void report('native-storage-read');
        return result;
    };
    runtime.clear = id => {
        storage.delete(id);
        requests.delete(id);
        return clear(id);
    };
    void (async () => {
        const key = 'desktop-smoke-' + crypto.randomUUID();
        const message = { referenceId: crypto.randomUUID(), content: 'runtime fixture',
            dateSent: new Date().toISOString() };
        await runtime.invoke(JSON.stringify({ method: 'history.put', args: [key, message, false] }));
        const response = await runtime.invoke(JSON.stringify({ method: 'history.get', args: [key, 1] }));
        if (JSON.parse(response).value?.[0]?.content === message.content) await report('indexeddb-roundtrip');
        const plain = btoa(message.content);
        const encrypted = await egrooCrypto.encryptFile({ fileBase64: plain });
        if (await egrooCrypto.decryptFile(encrypted) === plain) await report('webcrypto-roundtrip');
    })().catch(() => report('runtime-error'));
})();`;

export async function startRecorder(webroot) {
    const html = await readFile(join(webroot, 'runtime.html'), 'utf8');
    const scripts = Array.from(html.matchAll(/<script src="([\w.-]+\.js)">/g), item => item[1]);
    assert.ok(scripts.includes('clientRuntime.js'), 'The source runtime HTML is incomplete.');
    const files = new Map(await Promise.all(scripts.map(async name =>
        ['/runtime/' + name, await readFile(join(webroot, 'js', name))])));
    files.set('/runtime/proof.js', runtimeProof);
    const page = html.replace("default-src 'none';", "default-src 'none'; connect-src 'self';")
        .replace('</body>', '    <script src="proof.js"></script>\n</body>');
    const required = ['runtime-index', 'native-storage-read', 'indexeddb-roundtrip', 'webcrypto-roundtrip'];
    const observations = new Set();
    const recorder = createServer((request, response) => {
        response.setHeader('Cache-Control', 'no-store');
        if (request.method === 'GET' && request.url === '/runtime/index.html') {
            observations.add('runtime-index');
            return response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }).end(page);
        }
        if (request.method === 'GET' && files.has(request.url))
            return response.writeHead(200, { 'Content-Type': 'text/javascript; charset=utf-8' }).end(files.get(request.url));
        if (request.method === 'POST' && request.url.startsWith('/smoke/')) {
            const marker = request.url.slice('/smoke/'.length);
            if (required.includes(marker) || marker === 'runtime-error') {
                observations.add(marker);
                return response.writeHead(204).end();
            }
        }
        return response.writeHead(404).end();
    });
    recorder.listen(0, '127.0.0.1');
    await once(recorder, 'listening');
    return {
        url: 'http://127.0.0.1:' + recorder.address().port + '/',
        observations, required,
        close() {
            return new Promise((complete, reject) => {
                recorder.close(error => error ? reject(error) : complete());
                recorder.closeAllConnections();
            });
        }
    };
}

const launcherSource = `
import AppKit
import Foundation

let configuration = NSWorkspace.OpenConfiguration()
configuration.createsNewApplicationInstance = true
configuration.addsToRecentItems = false
configuration.activates = false
configuration.promptsUserIfNeeded = false
configuration.environment = ["EGROO_API_URL": CommandLine.arguments[2]]
let app = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
NSWorkspace.shared.openApplication(at: app, configuration: configuration) { running, error in
    if let running = running {
        print("EGROO_PID=\u005c(running.processIdentifier)")
        exit(0)
    }
    let message = "\u005c(String(describing: error))\u005cn"
    FileHandle.standardError.write(Data(message.utf8))
    exit(1)
}
RunLoop.main.run()
`;

async function main() {
    assert.equal(process.platform, 'darwin', 'This check requires a macOS runner.');
    assert.equal(process.arch, 'arm64', 'Use an Apple Silicon macOS runner.');
    const [appPath, webroot, evidencePath] = process.argv.slice(2);
    assert.ok(appPath && webroot && evidencePath,
        'Pass the .app bundle, source Egroo.UI/wwwroot directory, and evidence JSON path.');
    const app = resolve(appPath);
    const executable = join(app, 'Contents', 'MacOS', 'Egroo');
    await access(executable, constants.X_OK);
    await run('plutil', ['-lint', join(app, 'Contents', 'Info.plist')]);
    await run('codesign', ['--verify', '--deep', '--strict', app]);

    const scratch = await mkdtemp(join(tmpdir(), 'egroo-macos-smoke-'));
    let recorder;
    let pid;
    try {
        const launcher = join(scratch, 'launch.swift');
        await writeFile(launcher, launcherSource);
        recorder = await startRecorder(webroot);
        const { stdout } = await run('xcrun', ['swift', launcher, app, recorder.url], { timeout: 45_000 });
        pid = Number(stdout.match(/^EGROO_PID=(\d+)$/m)?.[1]);
        assert.ok(Number.isSafeInteger(pid) && pid > 0, 'LaunchServices did not return the app PID.');
        const appIsRunning = () => {
            try {
                process.kill(pid, 0);
                return true;
            } catch {
                return false;
            }
        };
        const deadline = Date.now() + 45_000;
        while (recorder.required.some(marker => !recorder.observations.has(marker))
            && !recorder.observations.has('runtime-error') && appIsRunning() && Date.now() < deadline) {
            await delay(250);
        }
        const evidence = { launchPid: pid, running: appIsRunning(), observed: [...recorder.observations] };
        await writeFile(evidencePath, JSON.stringify(evidence, null, 2) + '\n');
        console.log(JSON.stringify(evidence));
        assert.ok(evidence.running, 'The packaged app exited during its runtime smoke.');
        assert.ok(recorder.required.every(marker => recorder.observations.has(marker)),
            'The packaged app did not complete the WKWebView and native bridge smoke.');
    } finally {
        if (pid) {
            try { process.kill(pid); } catch { /* The app already exited. */ }
        }
        await recorder?.close();
        await rm(scratch, { recursive: true, force: true });
    }
}

if (resolve(process.argv[1] ?? '.') === fileURLToPath(import.meta.url))
    await main();
