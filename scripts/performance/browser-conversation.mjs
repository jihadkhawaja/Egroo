import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { cpus, platform, release, arch } from 'node:os';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from 'playwright-core';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { PNG } from 'pngjs';
import { startLoopbackFixture } from './loopback-fixture.mjs';

const [browserPublish, serverPublish] = process.argv.slice(2);
if (!browserPublish || !serverPublish) {
    throw new Error('Pass the published browser wwwroot directory and the published API directory.');
}

const viewport = { width: 1280, height: 900 };
const historySize = 150;
const sampleCount = 32;
const fixture = await startLoopbackFixture(browserPublish, serverPublish);
let browser;
let sender;
try {
    const { api, origin } = fixture;
    const alice = await register(api, 'perf_alice');
    const bob = await register(api, 'perf_bob');
    sender = new HubConnectionBuilder()
        .withUrl(api + '/chathub', {
            transport: HttpTransportType.WebSockets,
            skipNegotiation: true,
            accessTokenFactory: () => alice.token
        })
        .configureLogging(LogLevel.None)
        .build();
    await sender.start();
    const channel = await sender.invoke('CreateChannel', ['perf_alice', 'perf_bob']);
    assert.ok(channel?.id, 'Fixture channel was not created.');

    browser = await chromium.launch(process.env.EGROO_PERF_CHROMIUM
        ? { executablePath: process.env.EGROO_PERF_CHROMIUM }
        : { channel: 'chrome' });
    const context = await browser.newContext({ viewport, deviceScaleFactor: 1, colorScheme: 'light' });
    const page = await context.newPage();
    await page.addInitScript(({ api, token, origin }) => {
        if (location.origin !== origin) return;
        localStorage.setItem('egroo.session:' + api + '/', token);
        globalThis.egrooFixtureLongTasks = [];
        new PerformanceObserver(entries => {
            globalThis.egrooFixtureLongTasks.push(...entries.getEntries().map(entry => entry.duration));
        }).observe({ type: 'longtask', buffered: true });

        const NativeWebSocket = globalThis.WebSocket;
        let chatSocket;
        globalThis.WebSocket = class extends NativeWebSocket {
            constructor(url, protocols) {
                super(url, protocols);
                if (new URL(url).pathname === '/chathub') chatSocket = this;
            }
        };
        globalThis.egrooFixtureDisconnect = () => new Promise(resolve => {
            if (!chatSocket || chatSocket.readyState === WebSocket.CLOSED) {
                resolve();
                return;
            }

            chatSocket.addEventListener('close', () => resolve(), { once: true });
            chatSocket.close(1000, 'Loopback reconnect benchmark');
        });
    }, { api, token: bob.token, origin });

    const startupReplay = waitForPendingReplay(page);
    const started = Date.now();
    await Promise.all([
        page.goto(origin),
        page.locator('#egroo[data-egroo-ready=true]').waitFor({ timeout: 45_000 }),
        startupReplay
    ]);
    const workspaceMs = Date.now() - started;

    const members = await sender.invoke('GetChannelUsers', channel.id);
    const recipient = members.find(item => item.id === bob.userId);
    assert.ok(recipient?.encryptionKeys?.length, 'The browser device did not enroll an encryption key.');
    const recipients = [{ userId: bob.userId, keys: recipient.encryptionKeys }];
    const encrypt = plaintext => page.evaluate(({ plaintext, recipients }) =>
        globalThis.egrooCrypto.encryptMessageForRecipients({ plaintext, recipients }), { plaintext, recipients });
    const seed = await encrypt('History fixture content');
    const scope = api + '/' + bob.userId.replaceAll('-', '') + '/' + channel.id.replaceAll('-', '');
    await seedHistory(page, scope, channel.id, alice.userId, seed[0].content);

    const display = await observeDisplay(context, page);
    const firstTimeline = display.changed(await display.capture(), populatedTimeline);
    await page.mouse.click(400, 333);
    const firstTimelineMs = await firstTimeline;
    await delay(250);
    const afterOpen = await display.metrics();

    let readReceipt;
    sender.on('UpdateMessage', message => readReceipt?.(message));
    const samples = [];
    for (let n = 1; n <= sampleCount; n++) {
        const attachment = n % 8 === 0;
        const file = attachment ? await prepareAttachment(page, api, alice.token, channel.id, n) : null;
        const contents = await encrypt('Rendered fixture message ' + n + (file?.token ?? ''));
        const id = randomUUID();
        const before = await display.capture();
        const sentAt = Date.now();
        const screen = display.changed(before);
        const receipt = new Promise((resolve, reject) => {
            const deadline = setTimeout(() => reject(new Error('The sender did not get a read receipt.')), 10_000);
            readReceipt = message => {
                if (message.referenceId !== id) return;
                clearTimeout(deadline);
                resolve(Date.now() - sentAt);
            };
        });
        const [frameMs, receiptMs, accepted] = await Promise.all([screen, receipt, send(id, contents)]);
        assert.equal(accepted, true, 'The API did not accept an encrypted message.');
        samples.push({ attachment, frameMs, receiptMs });
        await delay(120);
        if (file?.previewUrl) {
            const response = page.waitForResponse(item => item.url() === file.previewUrl && item.ok(), { timeout: 10_000 });
            await page.mouse.click(367, 726);
            await (await response).finished();
            await delay(250);
            await page.mouse.move(430, 630);
            await page.mouse.wheel(0, 1000);
            await delay(120);
        }
    }
    const afterMessages = await display.metrics();

    // The API remains available to the sender while the receiver loses its connection.
    const offlineMessages = await Promise.all([encrypt('Queued fixture one'), encrypt('Queued fixture two')]);
    const queuedIds = offlineMessages.map(() => randomUUID());
    await context.setOffline(true);
    await page.evaluate(() => globalThis.egrooFixtureDisconnect());
    for (let i = 0; i < offlineMessages.length; i++)
        assert.equal(await send(queuedIds[i], offlineMessages[i]), true);
    const reconnectReplay = waitForPendingReplay(page);
    const reconnectStarted = Date.now();
    await context.setOffline(false);
    await reconnectReplay;
    await page.waitForFunction(async ({ scope, queuedIds }) => {
        const result = JSON.parse(await globalThis.egrooRuntime.invoke(
            JSON.stringify({ method: 'history.get', args: [scope, 2] })));
        return queuedIds.every((id, i) => result.value?.[i]?.referenceId === id);
    }, { scope, queuedIds }, { timeout: 20_000 });
    const reconnectToPersistMs = Date.now() - reconnectStarted;

    const reopenSamplesMs = [];
    for (let n = 0; n < 3; n++) {
        const closed = display.changed(await display.capture(), frame => !populatedTimeline(frame));
        await page.mouse.click(275, 99);
        await closed;

        const reopened = display.changed(await display.capture(), populatedTimeline);
        await page.mouse.click(400, 333);
        reopenSamplesMs.push(await reopened);
        await delay(250);
    }
    const afterReopens = await display.metrics();

    await page.mouse.click(191, 857);
    await page.waitForFunction(apiUrl => localStorage.getItem('egroo.session:' + apiUrl + '/') === null, api,
        { timeout: 10_000 });
    const afterSignOut = await display.metrics();
    await display.stop();
    const longTasks = await page.evaluate(() => globalThis.egrooFixtureLongTasks);
    console.log(JSON.stringify({
        environment: { os: platform(), release: release(), architecture: arch(), cpu: cpus()[0]?.model,
            node: process.version, chromium: browser.version(), network: 'loopback' },
        fixture: { viewport, historySize, sampleCount, attachments: samples.filter(sample => sample.attachment).length },
        startup: { workspaceMs, firstTimelineMs },
        live: { frameMs: percentiles(samples.map(s => s.frameMs)), receiptMs: percentiles(samples.map(s => s.receiptMs)), samples },
        reconnectToPersistMs,
        reopenSamplesMs,
        browserLongTasksMs: longTasks,
        chromeHeapAfterOpening: afterOpen,
        chromeHeapAfterMessages: afterMessages,
        chromeHeapAfterReopens: afterReopens,
        chromeHeapAfterSignOut: afterSignOut
    }, null, 2));

    function send(referenceId, payload) {
        return sender.invoke('SendMessage', {
            senderId: alice.userId, channelId: channel.id, referenceId, displayName: 'Perf Alice',
            recipientContents: payload.map(({ userId, content }) => ({ userId, content }))
        });
    }
} finally {
    try {
        await Promise.allSettled([browser?.close(), sender?.stop()]);
    } finally {
        await fixture.close();
    }
}

async function register(api, username) {
    const response = await fetch(api + '/api/v1/Auth/signup', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password: 'PerfFixture!' + randomUUID() })
    });
    assert.equal(response.status, 200);
    const account = await response.json();
    assert.equal(account.success, true, account.message);
    return account;
}

function waitForPendingReplay(page) {
    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            page.off('websocket', watch);
            reject(new Error('The browser did not request pending messages.'));
        }, 45_000);
        function watch(socket) {
            socket.on('framesent', ({ payload }) => {
                if (typeof payload !== 'string' || !payload.includes('SendPendingMessages')) return;
                clearTimeout(timeout);
                page.off('websocket', watch);
                resolve();
            });
        }
        page.on('websocket', watch);
    });
}

async function seedHistory(page, scope, channelId, senderId, content) {
    const imported = await page.evaluate(async ({ scope, channelId, senderId, content, historySize }) => {
        const now = Date.now();
        for (let i = 0; i < historySize; i++) {
            const message = {
                channelId, senderId, id: crypto.randomUUID(), referenceId: crypto.randomUUID(),
                displayName: 'Perf Alice', dateSent: new Date(now - (historySize + 1 - i) * 1000).toISOString(), content
            };
            const result = JSON.parse(await globalThis.egrooRuntime.invoke(
                JSON.stringify({ method: 'history.put', args: [scope, message, false] })));
            if (result.error) return result.error;
        }
        return null;
    }, { scope, channelId, senderId, content, historySize });
    assert.equal(imported, null, 'Could not seed encrypted browser history.');
}

async function prepareAttachment(page, api, token, channelId, index) {
    const image = index % 16 === 0;
    const filename = image ? 'fixture.png' : 'fixture.txt';
    const contentType = image ? 'image/png' : 'text/plain';
    const file = image ? fixturePng() : Buffer.from(('Fixture attachment ' + index + '\n').repeat(2700));
    const encrypted = await page.evaluate(fileBase64 => globalThis.egrooCrypto.encryptFile({ fileBase64 }),
        file.toString('base64'));
    const form = new FormData();
    form.set('file', new Blob([Buffer.from(encrypted.encryptedBase64, 'base64')], { type: 'application/octet-stream' }),
        'fixture-' + index + '.bin');
    const response = await fetch(api + '/api/v1/ChannelFiles/' + channelId, {
        method: 'POST', headers: { Authorization: 'Bearer ' + token }, body: form
    });
    assert.equal(response.status, 200, 'The API did not store the encrypted attachment.');
    const stored = await response.json();
    const url = new URL(stored.url, api).href;
    const fileToken = Buffer.from(JSON.stringify({
        V: 1, Url: url, FileName: filename,
        ContentType: contentType, SizeBytes: file.length, Key: encrypted.keyBase64, Iv: encrypted.ivBase64
    })).toString('base64url');
    return { token: ' [[egroo-file:' + fileToken + ']]', previewUrl: image ? url : null };
}

function fixturePng() {
    const image = new PNG({ width: 256, height: 160 });
    for (let i = 0; i < image.data.length; i += 4) {
        image.data[i] = (i / 4) % image.width < image.width / 2 ? 35 : 220;
        image.data[i + 1] = 110;
        image.data[i + 2] = 160;
        image.data[i + 3] = 255;
    }
    return PNG.sync.write(image);
}

async function observeDisplay(context, page) {
    const cdp = await context.newCDPSession(page);
    const readPng = data => PNG.sync.read(Buffer.from(data, 'base64'));
    let sample;
    cdp.on('Page.screencastFrame', ({ data, metadata, sessionId }) => {
        void cdp.send('Page.screencastFrameAck', { sessionId }).catch(() => {});
        sample?.(data, metadata.timestamp * 1000);
    });
    await cdp.send('Page.startScreencast', { format: 'png', everyNthFrame: 1, maxWidth: viewport.width, maxHeight: viewport.height });
    return {
        capture: async () => readPng((await cdp.send('Page.captureScreenshot', { format: 'png' })).data),
        changed: (before, check = () => true) => {
            const region = timelineRegion(before);
            const started = Date.now();
            return new Promise((resolve, reject) => {
                const timer = setTimeout(() => {
                    sample = null;
                    reject(new Error('The expected timeline frame did not change.'));
                }, 15_000);
                sample = (data, at) => {
                    if (at < started) return;
                    const frame = readPng(data);
                    if (timelineRegion(frame) === region || !check(frame)) return;
                    clearTimeout(timer);
                    sample = null;
                    resolve(at - started);
                };
            });
        },
        metrics: () => cdp.send('Runtime.getHeapUsage'),
        stop: () => cdp.send('Page.stopScreencast')
    };
}

function timelineRegion(frame) {
    assert.equal(frame.width, viewport.width, 'Unexpected browser screencast size.');
    const hash = createHash('sha256');
    for (let y = 342; y < 724; y++) {
        const row = y * frame.width * 4;
        hash.update(frame.data.subarray(row + 284 * 4, row + 1195 * 4));
    }
    return hash.digest('hex');
}

function populatedTimeline(frame) {
    // At this viewport the lower timeline strip turns white when seeded bubbles are painted.
    let white = 0;
    for (let y = 640; y < 728; y++) {
        const pixel = (y * frame.width + 355) * 4;
        if (frame.data[pixel] === 255 && frame.data[pixel + 1] === 255 && frame.data[pixel + 2] === 255) white++;
    }
    return white >= 25;
}

function percentiles(values) {
    const sorted = [...values].sort((a, b) => a - b);
    const at = quantile => sorted[Math.ceil(quantile * sorted.length) - 1];
    return { median: at(0.5), p95: at(0.95) };
}
