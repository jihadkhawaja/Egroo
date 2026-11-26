import assert from 'node:assert/strict';
import { cpus, platform, release, arch } from 'node:os';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const origin = 'http://127.0.0.1';
const scope = 'fixture-api/fixture-user/fixture-channel';
const historySize = 15_000;
const pageSize = 150;
const iterations = 9;
const fixtureDate = Date.UTC(2026, 0, 1);
const runtimePath = process.env.EGROO_PERF_RUNTIME
    || fileURLToPath(new URL('../../src/Egroo.UI/wwwroot/js/clientRuntime.js', import.meta.url));
const args = process.argv.slice(2);
const forceCursor = args.includes('--cursor');
const baselinePath = args.find(arg => arg !== '--cursor');

const browser = await chromium.launch(process.env.EGROO_PERF_CHROMIUM
    ? { executablePath: process.env.EGROO_PERF_CHROMIUM }
    : { channel: 'chrome' });

try {
    const baseline = baselinePath ? await measure(baselinePath) : null;
    const candidate = await measure(runtimePath, forceCursor);

    const summarize = ({ firstPageMs, warmPageMs, directionalApiAvailable }) => ({
        directionalApiAvailable,
        firstPageMs,
        warmMedianMs: [...warmPageMs].sort((a, b) => a - b)[Math.floor(warmPageMs.length / 2)],
        warmPageMs
    });
    console.log(JSON.stringify({
        environment: { os: platform(), release: release(), architecture: arch(), cpu: cpus()[0]?.model,
            node: process.version, chromium: browser.version(), backend: 'local IndexedDB' },
        fixture: { historySize, pageSize, iterations, legacyDatabaseVersion: 1, forceCursor },
        ...(baseline && { baseline: summarize(baseline) }),
        candidate: summarize(candidate)
    }, null, 2));
} finally {
    await browser.close();
}

async function measure(runtime, useCursor = false) {
    const context = await browser.newContext();
    try {
        // Keep storage behavior real and serve only the fixture document. No API is involved.
        await context.route(origin + '/', route => route.fulfill({
            contentType: 'text/html',
            body: '<!doctype html><title>Egroo history benchmark</title>'
        }));
        const page = await context.newPage();
        await page.goto(origin);
        if (useCursor)
            await page.evaluate(() => { IDBIndex.prototype.getAllRecords = undefined; });
        await page.evaluate(({ scope, historySize, pageSize, fixtureDate }) => new Promise((resolve, reject) => {
            const request = indexedDB.open('EgrooNative', 1);
            request.onupgradeneeded = () => request.result.createObjectStore('cache');
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                const db = request.result;
                const tx = db.transaction('cache', 'readwrite');
                const store = tx.objectStore('cache');
                for (let i = 0; i < historySize; i++) {
                    // Key order differs from send order, as with application reference IDs.
                    const referenceId = String(historySize - i).padStart(6, '0');
                    // Put a timestamp tie across the page boundary to exercise index ordering.
                    const second = i === historySize - pageSize ? i - 1 : i;
                    const dateSent = new Date(fixtureDate + second * 1000).toISOString();
                    store.put({ referenceId, dateSent, content: 'x'.repeat(512) }, scope + '/' + referenceId);
                }
                store.put({ referenceId: 'other', dateSent: new Date(fixtureDate).toISOString(), content: 'other' },
                    scope + '-other/other');
                store.put(true, 'legacy-import:' + scope);
                tx.oncomplete = () => { db.close(); resolve(); };
                tx.onabort = () => { db.close(); reject(tx.error); };
            };
        }), { scope, historySize, pageSize, fixtureDate });
        await page.addScriptTag({ content: 'globalThis.webrtcInterop = { registerDotNetObject() {} }; globalThis.egrooCrypto = {};' });
        await page.addScriptTag({ path: runtime });

        const measured = await page.evaluate(async ({ scope, pageSize, iterations }) => {
            const invoke = async (method, ...args) => {
                const response = JSON.parse(await globalThis.egrooRuntime.invoke(JSON.stringify({ method, args })));
                if (response.error) throw new Error(response.error);
                return response.value;
            };
            const readPage = async () => {
                const started = performance.now();
                const records = await invoke('history.get', scope, pageSize);
                return { ms: performance.now() - started, ids: records.map(row => row.referenceId) };
            };

            const first = await readPage();
            const warm = [];
            for (let i = 0; i < iterations; i++) warm.push(await readPage());
            const unrelated = await invoke('history.get', scope + '-other', pageSize);
            const empty = await invoke('history.get', scope + '-missing', pageSize);
            const zero = await invoke('history.get', scope, 0);
            const all = await invoke('history.get', scope, null);

            const db = await new Promise((resolve, reject) => {
                const request = indexedDB.open('EgrooNative');
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
            const marker = await new Promise((resolve, reject) => {
                const request = db.transaction('cache').objectStore('cache').get('legacy-import:' + scope);
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
            db.close();

            return { firstPageMs: first.ms, warmPageMs: warm.map(sample => sample.ms),
                firstPage: first.ids, lastPage: warm.at(-1).ids, unrelated: unrelated.map(row => row.referenceId),
                total: all.length, marker, empty, zero,
                directionalApiAvailable: typeof IDBIndex.prototype.getAllRecords === 'function' };
        }, { scope, pageSize, iterations });

        const expected = Array.from({ length: pageSize }, (_, index) => String(pageSize - index).padStart(6, '0'));
        expected[0] = String(pageSize + 1).padStart(6, '0');
        assert.deepEqual(measured.firstPage, expected);
        assert.deepEqual(measured.lastPage, expected);
        assert.deepEqual(measured.unrelated, ['other']);
        assert.deepEqual(measured.empty, []);
        assert.deepEqual(measured.zero, []);
        assert.equal(measured.total, historySize);
        assert.equal(measured.marker, true);
        return measured;
    } finally {
        await context.close();
    }
}
