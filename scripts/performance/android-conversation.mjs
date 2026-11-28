import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { promisify } from 'node:util';
import { runInNewContext } from 'node:vm';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { startLoopbackFixture } from './loopback-fixture.mjs';

const [serial, releaseApk, smokeApk, serverPublish, artifacts] = process.argv.slice(2);
assert.match(serial ?? '', /^emulator-\d+$/, 'Use a disposable Android emulator.');
assert.ok(releaseApk && smokeApk && serverPublish && artifacts,
    'Pass the emulator serial, release APK, smoke APK, API publish directory and artifact directory.');

const releasePackage = 'com.jihadkhawaja.egroo';
const smokePackage = releasePackage + '.smoke';
const devicePort = 'tcp:5187';
const historyCount = 150;
const sampleCount = 8;
const hierarchy = '/sdcard/egroo-smoke-window.xml';
const command = promisify(execFile);

async function adb(...args) {
    try {
        const launchOrInstall = args[0] === 'install' || (args[0] === 'shell' && args[1] === 'am' && args[2] === 'start');
        const { stdout } = await command(process.env.EGROO_ADB ?? 'adb', ['-s', serial, ...args],
            { encoding: 'buffer', timeout: launchOrInstall ? 60_000 : 15_000,
                maxBuffer: 8 * 1024 * 1024, windowsHide: true });
        return stdout;
    } catch (failure) {
        // Input command arguments include a temporary password. Keep them out of CI logs.
        const [operation, subcommand] = args;
        const details = subcommand === 'input' ? '' : failure.stderr?.toString().trim();
        throw new Error(`adb ${operation} failed${details ? ': ' + details : '.'}`);
    }
}

async function shell(...args) {
    return (await adb('shell', ...args)).toString().trim();
}

async function startApp(packageName) {
    const resolution = await shell('cmd', 'package', 'resolve-activity', '--brief',
        '-a', 'android.intent.action.MAIN', '-c', 'android.intent.category.LAUNCHER', packageName);
    const activity = resolution.split(/\r?\n/).at(-1);
    assert.ok(activity.startsWith(packageName + '/'), 'The installed APK has no launcher activity.');
    const result = await shell('am', 'start', '-W', '-n', activity);
    assert.match(result, /^Status:\s*ok\s*$/mi, 'Android did not start the launcher activity.');
}

async function screen(packageName) {
    const dump = await shell('uiautomator', 'dump', hierarchy);
    assert.ok(dump.includes(hierarchy), 'Android did not write a new UI hierarchy.');
    const xml = (await adb('exec-out', 'cat', hierarchy)).toString();
    assert.ok(xml.includes('<hierarchy'), 'Android did not return a UI hierarchy.');
    const nodes = [];
    for (const matched of xml.matchAll(/<node\s+([^>]+)>/g)) {
        const attributes = Object.fromEntries(Array.from(matched[1].matchAll(/([\w-]+)="([^"]*)"/g),
            ([, name, value]) => [name, value]));
        const bounds = attributes.bounds?.match(/^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$/)?.slice(1).map(Number);
        if (attributes.package === packageName && bounds && bounds[2] > bounds[0] && bounds[3] > bounds[1]) {
            nodes.push({ attributes, bounds });
        }
    }
    return nodes;
}

function hasLabel(node, label) {
    const { attributes } = node;
    return attributes.text === label || attributes['content-desc'] === label;
}

function control(nodes, label, type) {
    const matching = nodes.filter(node => hasLabel(node, label));
    if (type === 'edit')
        return matching.find(node => ['TextBox', 'android.widget.EditText'].includes(node.attributes.class));
    if (type === 'button')
        return matching.find(node => node.attributes.class === 'Button' || node.attributes.clickable === 'true');
    return matching[0];
}

async function until(description, operation, timeout = 60_000) {
    const deadline = performance.now() + timeout;
    do {
        const result = await operation();
        if (result) return result;
        await delay(250);
    } while (performance.now() < deadline);
    throw new Error('Timed out waiting for ' + description + '.');
}

async function waitForControl(packageName, label, type) {
    return until(label, async () => control(await screen(packageName).catch(() => []), label, type));
}

async function tap(node) {
    const [left, top, right, bottom] = node.bounds;
    await shell('input', 'tap', String(Math.round((left + right) / 2)), String(Math.round((top + bottom) / 2)));
}

async function input(text) {
    assert.match(text, /^[a-z\d_! ]+$/i, 'Only fixture text can be entered by ADB.');
    await shell('input', 'text', text.replaceAll(' ', '%s'));
}

async function dismissIme() {
    const state = await shell('dumpsys', 'input_method');
    if (/\bmInputShown=true\b/.test(state))
        await shell('input', 'keyevent', 'KEYCODE_BACK');
}

async function register(api, username) {
    const password = 'AndroidFixture9!' + randomUUID().replaceAll('-', '');
    const response = await fetch(api + '/api/v1/Auth/signup', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
        signal: AbortSignal.timeout(15_000)
    });
    assert.equal(response.status, 200, 'The disposable API did not register a fixture account.');
    const body = await response.json();
    assert.ok(body.success && body.token && body.userId, 'The fixture account is incomplete.');
    return { username, password, token: body.token, userId: body.userId };
}

function connect(api, account) {
    return new HubConnectionBuilder().withUrl(api + '/chathub', {
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
        accessTokenFactory: () => account.token
    }).configureLogging(LogLevel.None).build();
}

async function requireAuthenticationScreen(packageName) {
    await until('native sign-in controls', async () => {
        const nodes = await screen(packageName).catch(() => []);
        return control(nodes, 'Sign in', 'button') && control(nodes, 'Username') && control(nodes, 'Password');
    });
}

async function appPssKiB(packageName) {
    const info = await shell('dumpsys', 'meminfo', packageName).catch(() => '');
    const pss = info.match(/\bTOTAL PSS:\s*(\d+)/)?.[1];
    return pss === undefined ? null : Number(pss);
}

const elapsed = start => Math.round(performance.now() - start);
const accountNames = { reader: 'android_reader', sender: 'android_sender' };
let fixture;
let peer;
let pendingProbe;
let reverseInstalled = false;
try {
    assert.ok(await shell('getprop', 'ro.kernel.qemu') === '1' || await shell('getprop', 'ro.boot.qemu') === '1',
        'Refusing to use a physical Android device.');
    assert.equal(await shell('settings', 'get', 'global', 'airplane_mode_on'), '1',
        'The disposable emulator must start with airplane mode enabled.');
    await mkdir(artifacts, { recursive: true });
    await shell('wm', 'dismiss-keyguard');

    await adb('install', releaseApk);
    const launchAt = performance.now();
    await startApp(releasePackage);
    await requireAuthenticationScreen(releasePackage);
    const offlineLaunchMs = elapsed(launchAt);
    await shell('pidof', releasePackage);
    await shell('am', 'force-stop', releasePackage);
    console.log('Default Release APK displayed the offline sign-in screen.');

    fixture = await startLoopbackFixture(null, serverPublish);
    const hostPort = 'tcp:' + new URL(fixture.api).port;
    await adb('reverse', '--no-rebind', devicePort, hostPort);
    reverseInstalled = true;
    const reader = await register(fixture.api, accountNames.reader);
    const sender = await register(fixture.api, accountNames.sender);
    peer = connect(fixture.api, sender);
    await peer.start();

    const cryptoSource = await readFile(new URL('../../src/Egroo.UI/wwwroot/js/egrooCrypto.js', import.meta.url), 'utf8');
    const context = { window: {}, TextEncoder, TextDecoder, crypto, btoa, atob, DOMException };
    runInNewContext(cryptoSource, context);
    const protocol = context.window.egrooCrypto;
    const identity = await protocol.generateIdentity();
    assert.equal(await peer.invoke('AddEncryptionKey', identity.publicKey, identity.keyId, 'Android smoke peer'), true);
    const channel = await peer.invoke('CreateChannel', [reader.username]);
    assert.ok(channel?.id, 'Could not create the fixture channel.');
    const title = channel.title || channel.defaultTitle || 'Channel';

    await adb('install', smokeApk);
    await startApp(smokePackage);
    await tap(await waitForControl(smokePackage, 'Username', 'edit'));
    await input(reader.username);
    await tap(await waitForControl(smokePackage, 'Password', 'edit'));
    await input(reader.password);
    await dismissIme();
    const submit = await waitForControl(smokePackage, 'Sign in', 'button');
    const signInAt = performance.now();
    await tap(submit);
    await waitForControl(smokePackage, title, 'button');
    const signInMs = elapsed(signInAt);
    const recipient = await until('Android encryption enrollment', async () => {
        const members = await peer.invoke('GetChannelUsers', channel.id);
        const readerKeys = members?.find(member => member.id === reader.userId)?.encryptionKeys;
        return readerKeys?.length ? { userId: reader.userId, keys: readerKeys } : null;
    });
    console.log('Native sign-in and Android encryption-key enrollment succeeded.');

    const receipts = new Map();
    const fromAndroid = [];
    peer.on('UpdateMessage', message => receipts.set(message.referenceId, performance.now()));
    peer.on('ReceiveMessage', message => {
        if (message.channelId === channel.id && message.senderId === reader.userId)
            fromAndroid.push(message);
    });

    async function send(plaintext) {
        const [{ content }] = await protocol.encryptMessageForRecipients({ plaintext, recipients: [recipient] });
        const referenceId = randomUUID();
        const sentAt = performance.now();
        assert.equal(await peer.invoke('SendMessage', {
            referenceId, senderId: sender.userId, channelId: channel.id, displayName: 'Android Smoke Peer',
            recipientContents: [{ userId: reader.userId, content }]
        }), true, 'The API declined a fixture message.');
        return { referenceId, sentAt };
    }

    const seedIds = new Set();
    for (let sample = 1; sample <= historyCount; sample++) {
        const text = 'Android history ' + sample + (sample % 7 === 0 ? ' Variable height history content'.repeat(5) : '');
        const { referenceId } = await send(text);
        seedIds.add(referenceId);
    }
    pendingProbe = connect(fixture.api, reader);
    await pendingProbe.start();
    const pending = new Set();
    pendingProbe.on('ReceiveMessage', message => pending.add(message.referenceId));
    await until('Android caching and acknowledging the encrypted history', async () => {
        pending.clear();
        await pendingProbe.invoke('SendPendingMessages');
        return [...seedIds].every(id => !pending.has(id));
    }, 120_000);
    await pendingProbe.stop();
    pendingProbe = null;
    console.log(`Android cached and acknowledged all ${historyCount} encrypted history messages.`);

    let channelButton = await waitForControl(smokePackage, title, 'button');
    const openAt = performance.now();
    await tap(channelButton);
    await waitForControl(smokePackage, 'Android history ' + historyCount);
    const coldConversationMs = elapsed(openAt);
    const live = [];
    for (let sample = 1; sample <= sampleCount; sample++) {
        const message = 'Android live fixture ' + sample;
        const { referenceId, sentAt } = await send(message);
        const [observedAt, receiptAt] = await Promise.all([
            waitForControl(smokePackage, message).then(() => performance.now()),
            until('sender receipt for sample ' + sample, () => receipts.get(referenceId))
        ]);
        live.push({ accessibilityObservationMs: Math.round(observedAt - sentAt), senderReceiptMs: Math.round(receiptAt - sentAt) });
    }

    const reply = 'Native Android reply';
    await tap(await waitForControl(smokePackage, 'Message', 'edit'));
    await input(reply);
    await dismissIme();
    await tap(await waitForControl(smokePackage, 'Send', 'button'));
    const received = await until('an encrypted reply from Android', () => fromAndroid[0], 45_000);
    const decrypted = await protocol.decryptMessage({ payload: received.content, privateKey: identity.privateKey });
    assert.equal(decrypted.status, 'decrypted');
    assert.equal(decrypted.plaintext, reply);
    await waitForControl(smokePackage, reply);
    await writeFile(join(artifacts, 'android-chat.png'), await adb('exec-out', 'screencap', '-p'));
    const beforeRestartPssKiB = await appPssKiB(smokePackage);
    console.log('Android rendered encrypted live messages and sent an encrypted reply.');

    await shell('am', 'force-stop', smokePackage);
    const restoreAt = performance.now();
    await startApp(smokePackage);
    channelButton = await waitForControl(smokePackage, title, 'button');
    const restoreSessionMs = elapsed(restoreAt);
    await tap(channelButton);
    await waitForControl(smokePackage, reply);
    const afterReopenPssKiB = await appPssKiB(smokePackage);
    const webView = (await shell('dumpsys', 'webviewupdate')).match(/Current WebView package \(name, version\): \(([^)]+)\)/)?.[1] ?? null;

    const metrics = {
        android: await shell('getprop', 'ro.build.version.release'),
        apiLevel: Number(await shell('getprop', 'ro.build.version.sdk')),
        abi: await shell('getprop', 'ro.product.cpu.abi'),
        webView,
        viewportPixels: (await shell('wm', 'size')).split(/\r?\n/).at(-1).split(': ').at(-1),
        densityDpi: Number((await shell('wm', 'density')).split(/\r?\n/).at(-1).split(': ').at(-1)),
        historyCount, sampleCount, offlineLaunchMs, signInMs, coldConversationMs, restoreSessionMs, live,
        appMemory: { source: 'Android dumpsys meminfo TOTAL PSS (KiB)', beforeRestartPssKiB, afterReopenPssKiB },
        measurement: 'UI observations include Android UIAutomator accessibility dump and polling overhead.'
    };
    await writeFile(join(artifacts, 'android-conversation.json'), JSON.stringify(metrics, null, 2) + '\n');
    console.log(JSON.stringify(metrics, null, 2));
} finally {
    await Promise.allSettled([peer?.stop(), pendingProbe?.stop()]);
    if (reverseInstalled) await adb('reverse', '--remove', devicePort).catch(() => {});
    await fixture?.close();
}
