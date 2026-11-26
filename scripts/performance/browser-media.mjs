import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { arch, cpus, platform, release } from 'node:os';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from 'playwright-core';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { startLoopbackFixture } from './loopback-fixture.mjs';

const [browserPublish, serverPublish] = process.argv.slice(2);
if (!browserPublish || !serverPublish)
    throw new Error('Pass the published browser wwwroot directory and the published API directory.');

const viewport = { width: 1280, height: 900 };
const fixture = await startLoopbackFixture(browserPublish, serverPublish);
let browser;
let observer;
try {
    const { api, origin } = fixture;
    const alice = await register(api, 'media_alice');
    const bob = await register(api, 'media_bob');

    observer = new HubConnectionBuilder()
        .withUrl(api + '/chathub', {
            transport: HttpTransportType.WebSockets,
            skipNegotiation: true,
            accessTokenFactory: () => alice.token
        })
        .configureLogging(LogLevel.None)
        .build();
    await observer.start();
    const room = await observer.invoke('CreateChannel', ['media_bob']);
    assert.ok(room?.id, 'Fixture channel was not created.');

    browser = await chromium.launch({
        ...(process.env.EGROO_PERF_CHROMIUM ? { executablePath: process.env.EGROO_PERF_CHROMIUM } : { channel: 'chrome' }),
        args: ['--use-fake-device-for-media-stream', '--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1']
    });
    const [alicePage, bobPage] = await Promise.all([startPage(alice.token), startPage(bob.token)]);

    await verifyCaptureTransitions(alicePage);

    const joinStarted = Date.now();
    await openRoom(alicePage, 0, room.id);
    await alicePage.mouse.click(445, 152);
    await waitForVoice(alicePage, room.id);

    await openRoom(bobPage, 0, room.id);
    await bobPage.mouse.click(445, 152);
    await waitForVoice(bobPage, room.id);
    await Promise.all([waitForInboundAudio(alicePage), waitForInboundAudio(bobPage)]);
    const connectedMs = Date.now() - joinStarted;
    await waitForParticipants(room.id, [alice.userId, bob.userId]);
    const secondRoom = await observer.invoke('CreateChannel', ['media_bob']);
    assert.ok(secondRoom?.id && secondRoom.id !== room.id, 'Second fixture channel was not created.');

    const roomIndex = (await observer.invoke('GetUserChannels')).findIndex(channel => channel.id === room.id);
    assert.ok(roomIndex === 0 || roomIndex === 1, 'Call channel was missing from the fixture channel list.');
    for (let index = 0; index < 6; index++) {
        await alicePage.mouse.click(275, 99);
        await delay(250);
        const showCallRoom = index % 2 !== 0;
        await openRoom(alicePage, showCallRoom ? roomIndex : 1 - roomIndex, showCallRoom ? room.id : secondRoom.id);
    }

    const afterSwitches = await mediaSnapshot(alicePage);
    assert.equal(afterSwitches.channelId, room.id, 'Navigation should preserve the active call.');
    assert.equal(afterSwitches.liveLocal, 1);
    assert.equal(afterSwitches.activePeers, 1);
    assert.equal(afterSwitches.remoteAudio, 1);

    await alicePage.mouse.click(191, 857);
    await alicePage.waitForFunction(apiUrl => localStorage.getItem('egroo.session:' + apiUrl + '/') === null, api);
    await waitForReleasedMedia(alicePage);
    await bobPage.waitForFunction(() => globalThis.webrtcInterop.peers.size === 0 &&
        !document.querySelector('audio[id^=remoteAudio-]'));
    await waitForParticipants(room.id, [bob.userId]);

    await bobPage.mouse.click(453, 152);
    await waitForReleasedMedia(bobPage);
    await waitForParticipants(room.id, []);

    // A fresh call in the same browser context must open its own capture and release it again.
    await bobPage.mouse.click(445, 152);
    await waitForVoice(bobPage, room.id);
    await bobPage.mouse.click(453, 152);
    await waitForReleasedMedia(bobPage);

    console.log(JSON.stringify({
        environment: {
            os: platform(), release: release(), architecture: arch(), cpu: cpus()[0]?.model,
            chromium: browser.version(), viewport, network: 'loopback', source: 'Chromium synthetic microphone'
        },
        call: { connectedMs, channelSwitches: 6, bidirectionalRtp: true, afterSwitches },
        afterSignOut: await mediaSnapshot(alicePage),
        afterLeaveAndRejoin: await mediaSnapshot(bobPage),
        regressions: ['idle preferences', 'denial and retry', 'late permission after leave',
            'leave during track replacement', 'failed track replacement rollback']
    }, null, 2));

    async function waitForParticipants(channelId, users) {
        const deadline = Date.now() + 10_000;
        const expected = users.toSorted();
        let participants;
        do {
            participants = (await observer.invoke('GetChannelCallParticipants', channelId)).toSorted();
            if (JSON.stringify(participants) === JSON.stringify(expected)) return;
            await delay(50);
        } while (Date.now() < deadline);
        assert.deepEqual(participants, expected, 'Server call participation did not converge.');
    }

    async function startPage(token) {
        const context = await browser.newContext({ viewport, deviceScaleFactor: 1, colorScheme: 'light' });
        await context.grantPermissions(['microphone'], { origin });
        const page = await context.newPage();
        await page.addInitScript(({ api, token, origin }) => {
            if (location.origin !== origin) return;
            localStorage.setItem('egroo.session:' + api + '/', token);

            const media = navigator.mediaDevices;
            const getUserMedia = media.getUserMedia.bind(media);
            const tracks = [];
            const peers = [];
            const probe = globalThis.egrooMediaProbe = { requests: 0, tracks, peers, nextCapture: null, denyNext: false };
            media.getUserMedia = async constraints => {
                probe.requests++;
                if (probe.denyNext) {
                    probe.denyNext = false;
                    throw new DOMException('Disposable fixture permission denied', 'NotAllowedError');
                }
                const captureGate = probe.nextCapture;
                probe.nextCapture = null;
                const stream = await getUserMedia(constraints);
                tracks.push(...stream.getTracks());
                if (captureGate) {
                    captureGate.acquired();
                    await captureGate.release;
                }
                return stream;
            };

            // Observe actual history reads so each canvas click must open its intended channel.
            Object.defineProperty(globalThis, 'egrooRuntime', {
                configurable: true,
                set(runtime) {
                    const invoke = runtime.invoke;
                    runtime.invoke = async (request, id) => {
                        const result = await invoke(request, id);
                        const { method, args } = JSON.parse(request);
                        if (method === 'history.get')
                            probe.historyScope = args[0];
                        return result;
                    };
                    Object.defineProperty(globalThis, 'egrooRuntime', {
                        configurable: true, writable: true, value: runtime
                    });
                }
            });

            const PlatformPeer = globalThis.RTCPeerConnection;
            globalThis.RTCPeerConnection = class extends PlatformPeer {
                constructor(settings) {
                    super({ ...settings, iceServers: [], iceTransportPolicy: 'all' });
                    peers.push(this);
                }
            };
        }, { api, token, origin });

        const ready = page.waitForEvent('websocket', { timeout: 45_000 }).then(socket =>
            socket.waitForEvent('framesent', {
                predicate: frame => typeof frame.payload === 'string' && frame.payload.includes('SendPendingMessages'),
                timeout: 45_000
            }));
        await Promise.all([
            page.goto(origin),
            page.locator('#egroo[data-egroo-ready=true]').waitFor({ timeout: 45_000 }),
            ready
        ]);
        return page;
    }
} finally {
    try {
        await Promise.allSettled([browser?.close(), observer?.stop()]);
    } finally {
        await fixture.close();
    }
}

async function register(api, username) {
    const response = await fetch(api + '/api/v1/Auth/signup', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password: 'MediaFixture!' + randomUUID() })
    });
    assert.equal(response.status, 200);
    const account = await response.json();
    assert.equal(account.success, true, account.message);
    return account;
}

// Avalonia Browser paints onto a canvas. These points target the channel list at the fixed viewport.
async function openRoom(page, index, channelId) {
    await page.mouse.click(400, 333 + 63 * index);
    await page.waitForFunction(id => globalThis.egrooMediaProbe.historyScope?.endsWith(id), channelId.replaceAll('-', ''),
        { timeout: 10_000 });
}

async function waitForVoice(page, channelId) {
    await page.waitForFunction(id => {
        const voice = globalThis.webrtcInterop;
        return voice.channelId === id && voice.localStream?.getAudioTracks().some(track => track.readyState === 'live');
    }, channelId, { timeout: 10_000 });
}

async function waitForInboundAudio(page) {
    const deadline = Date.now() + 15_000;
    do {
        const received = await page.evaluate(async () => {
            const connections = [...globalThis.webrtcInterop.peers.values()];
            if (connections.length !== 1 || !document.querySelector('audio[id^=remoteAudio-]')?.srcObject)
                return false;
            const stats = await connections[0].pc.getStats();
            return [...stats.values()].some(report =>
                report.type === 'inbound-rtp' && report.kind === 'audio' && report.packetsReceived > 0);
        });
        if (received) return;
        await delay(100);
    } while (Date.now() < deadline);
    assert.fail('Did not receive audio over the peer connection.');
}

async function waitForReleasedMedia(page) {
    await page.waitForFunction(() => {
        const voice = globalThis.webrtcInterop;
        const probe = globalThis.egrooMediaProbe;
        return voice.channelId === null && voice.localStream === null && voice.statsInterval === null &&
            voice.peers.size === 0 && probe.tracks.every(track => track.readyState === 'ended') &&
            probe.peers.every(peer => peer.signalingState === 'closed') &&
            !document.querySelector('audio[id^=remoteAudio-]');
    }, null, { timeout: 10_000 });
}

async function mediaSnapshot(page) {
    return page.evaluate(() => {
        const voice = globalThis.webrtcInterop;
        const probe = globalThis.egrooMediaProbe;
        return {
            channelId: voice.channelId,
            liveLocal: probe.tracks.filter(track => track.readyState === 'live').length,
            endedLocal: probe.tracks.filter(track => track.readyState === 'ended').length,
            activePeers: probe.peers.filter(peer => peer.signalingState !== 'closed').length,
            remoteAudio: document.querySelectorAll('audio[id^=remoteAudio-]').length,
            statsTimer: voice.statsInterval !== null
        };
    });
}

async function verifyCaptureTransitions(page) {
    const result = await page.evaluate(async () => {
        const voice = globalThis.webrtcInterop;
        const probe = globalThis.egrooMediaProbe;
        const require = (condition, label) => { if (!condition) throw new Error(label); };
        const pendingCapture = () => {
            const captured = Promise.withResolvers();
            const released = Promise.withResolvers();
            probe.nextCapture = { acquired: captured.resolve, release: released.promise };
            return { acquired: captured.promise, release: released.resolve };
        };

        const preference = await voice.setAudioProcessingSetting('echoCancellation', false);
        require(preference.isEchoCancellationSupported, 'Chromium does not support the fixture constraint.');

        require(probe.requests === 0 && voice.localStream === null,
            'Changing idle settings opened the microphone.');

        probe.denyNext = true;
        require(!await voice.joinCall('denied', 'fixture') && voice.channelId === null,
            'Denied capture left an active call.');

        const capture = pendingCapture();
        const joining = voice.joinCall('late-join', 'fixture');
        await capture.acquired;
        voice.leaveCall();
        capture.release();
        require(!await joining && probe.tracks.every(track => track.readyState === 'ended'),
            'A permission result arriving after leave revived capture.');

        require(await voice.joinCall('replacement', 'fixture'), 'Expected a fresh call to acquire capture.');
        const before = voice.localStream.getAudioTracks()[0];
        const oldTracks = probe.tracks.length;

        // Use genuine MediaStreamTracks and control only the sender promises for these race/failure cases.
        const stuck = Promise.withResolvers();
        const started = Promise.withResolvers();
        const sender = {
            track: before,
            replaceTrack: async track => {
                sender.track = track;
                started.resolve();
                await stuck.promise;
            }
        };
        require(voice.peers.size === 0, 'Direct media-path verification must run before connecting peers.');
        voice.peers.set('paused-sender', {
            pc: { getSenders: () => [sender], close() {} },
            disconnectTimer: null
        });
        const updating = voice.setAudioProcessingSetting('echoCancellation', true);
        await started.promise;
        voice.leaveCall();
        require(probe.tracks.slice(oldTracks).every(track => track.readyState === 'ended'),
            'Leaving while replaceTrack was pending kept the replacement track alive.');
        stuck.resolve();
        await updating;
        require(voice.localStream === null && voice.statsInterval === null,
            'Finishing a replacement revived a closed call.');

        require(await voice.joinCall('failed-replacement', 'fixture'), 'Expected to recover after leaving during replacement.');
        const current = voice.localStream.getAudioTracks()[0];
        const previousRequests = probe.requests;
        const accepting = { track: current, async replaceTrack(track) { this.track = track; } };
        const rejecting = { track: current, async replaceTrack() { throw new DOMException('Fixture sender closed', 'InvalidStateError'); } };
        voice.peers.set('failed-sender', {
            pc: { getSenders: () => [accepting, rejecting], close() {} },
            disconnectTimer: null
        });
        const failed = await voice.setAudioProcessingSetting('echoCancellation', false);
        require(failed.wasApplied === false && probe.requests === previousRequests + 1 &&
            voice.localStream.getAudioTracks()[0] === current && accepting.track === current &&
            probe.tracks.at(-1).readyState === 'ended', 'A rejected sender replacement leaked or lost the old audio track.');
        voice.leaveCall();
        return probe.tracks.every(track => track.readyState === 'ended');
    });
    assert.equal(result, true);
}
