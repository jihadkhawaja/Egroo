(function () {
    const encoder = new TextEncoder();
    const decoder = new TextDecoder("utf-8", { fatal: true });
    const label = "egroo-nearby-v1";
    const prefix = "egroo-nearby-v1.";
    const maxCode = 32768;
    const maxMessage = 8000;
    const maxFrame = 6 * maxMessage + 64;
    let current;

    function encodeCode(description) {
        const bytes = encoder.encode(JSON.stringify({ type: description.type, sdp: description.sdp }));
        const code = prefix + btoa(String.fromCharCode(...bytes));
        if (code.length > maxCode) throw new Error("The pairing code is too large.");
        return code;
    }

    function decodeCode(code, type) {
        try {
            if (code.length > maxCode || !code.startsWith(prefix)) throw new Error();
            const bytes = Uint8Array.from(atob(code.slice(prefix.length)), char => char.charCodeAt(0));
            const description = JSON.parse(decoder.decode(bytes));
            if (description.type !== type || typeof description.sdp !== "string") throw new Error();
            const candidates = description.sdp.split(/\r?\n/).filter(line => line.startsWith("a=candidate:"));
            if (!candidates.length || candidates.some(line => !/\s+typ host(?:\s|$)/.test(line))) throw new Error();
            return { type, sdp: description.sdp };
        } catch {
            throw new Error(type === "offer" ? "Enter a valid Nearby invitation." : "Enter a valid Nearby reply.");
        }
    }

    function collectCandidates(pc) {
        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => finish(new Error("Local addresses took too long to gather.")), 10000);
            function finish(error) {
                clearTimeout(timeout);
                pc.removeEventListener("icegatheringstatechange", check);
                pc.removeEventListener("signalingstatechange", check);
                error ? reject(error) : resolve();
            }
            function check() {
                if (pc.signalingState === "closed") finish(new Error("Pairing was cancelled."));
                else if (pc.iceGatheringState === "complete") finish();
            }
            pc.addEventListener("icegatheringstatechange", check);
            pc.addEventListener("signalingstatechange", check);
            check();
        });
    }

    async function makeCode(session, description) {
        await session.pc.setLocalDescription(description);
        await collectCandidates(session.pc);
        const lines = session.pc.localDescription.sdp.trimEnd().split(/\r?\n/);
        const hostLines = lines.filter(line => !line.startsWith("a=candidate:") || /\s+typ host(?:\s|$)/.test(line));
        if (!hostLines.some(line => line.startsWith("a=candidate:")))
            throw new Error("No local network address was found. Connect to a shared network and try again.");
        return encodeCode({ type: description.type, sdp: hostLines.join("\r\n") + "\r\n" });
    }

    async function setVerificationCode(session, offer, answer) {
        // Hash the descriptions in role order so either peer sees any change to the exchanged SDP.
        const transcript = encoder.encode(JSON.stringify([label, offer, answer]));
        const hash = new Uint8Array(await crypto.subtle.digest("SHA-256", transcript));
        const hex = Array.from(hash.subarray(0, 16), byte => byte.toString(16).padStart(2, "0")).join("");
        session.verificationCode = hex.toUpperCase().match(/.{8}/g).join(" ");
        finishPairing(session);
    }

    function connected(session) {
        return !session.closed && session.confirmed === true
            && session.peerVerificationCode === session.verificationCode
            && session.channel?.readyState === "open";
    }

    function releaseVoice(session) {
        session.track?.stop();
        session.track = null;
        session.muted = false;
        session.audio.pause();
        session.audio.srcObject = null;
        session.playbackBlocked = false;
    }

    function updateVoiceSending(session) {
        if (session.track)
            session.track.enabled = session.peerVoice === true && !session.muted;
    }

    function close(session, reason) {
        if (!session || session.closed) return;
        session.closed = true;
        session.reason = reason;
        clearTimeout(session.pairingTimeout);
        clearTimeout(session.reconnectTimeout);
        releaseVoice(session);
        session.channel?.close();
        session.pc.close();
        session.audio.remove();
    }

    function requireConnected() {
        if (!current || !connected(current)) throw new Error("Verify and connect to the other device first.");
        return current;
    }

    function sendFrame(session, frame) {
        if (session.channel.bufferedAmount > 256 * 1024)
            throw new Error("The connection is busy. Try again in a moment.");
        session.channel.send(JSON.stringify(frame));
    }

    function finishPairing(session) {
        if (session.verificationCode && session.peerVerificationCode && session.peerVerificationCode !== session.verificationCode)
            close(session, "Verification codes differ. Start a new pairing.");
        else if (connected(session)) {
            session.wasConnected = true;
            clearTimeout(session.pairingTimeout);
        }
    }

    function attachChannel(session, channel) {
        if (session.channel || channel.label !== label) {
            channel.close();
            return;
        }
        session.channel = channel;
        function sendConfirmation() {
            if (!session.confirmed || session.confirmationSent) return;
            sendFrame(session, { type: "confirm", code: session.verificationCode });
            session.confirmationSent = true;
        }
        session.sendConfirmation = sendConfirmation;
        channel.onopen = () => {
            try {
                sendConfirmation();
                finishPairing(session);
            }
            catch { close(session, "Couldn’t verify the connection. Start a new pairing."); }
        };
        channel.onclose = () => close(session, "The other device disconnected.");
        channel.onerror = () => close(session, "The local connection failed.");
        channel.onmessage = ({ data }) => {
            if (session.closed || typeof data !== "string" || data.length > maxFrame) return;
            let frame;
            try { frame = JSON.parse(data); }
            catch { return; }
            if (frame?.type === "confirm" && typeof frame.code === "string" && /^(?:[A-F0-9]{8} ){3}[A-F0-9]{8}$/.test(frame.code)) {
                session.peerVerificationCode = frame.code;
                finishPairing(session);
            } else if (connected(session) && frame?.type === "message"
                && typeof frame.text === "string" && frame.text.trim() && frame.text.length <= maxMessage) {
                if (session.messages.length >= 256) {
                    close(session, "The other device sent messages faster than this device could display them.");
                    return;
                }
                session.messages.push(frame.text);
            } else if (connected(session) && frame?.type === "voice" && typeof frame.active === "boolean") {
                session.peerVoice = frame.active;
                updateVoiceSending(session);
            }
        };
    }

    function start() {
        if (!globalThis.isSecureContext || !globalThis.RTCPeerConnection || !globalThis.crypto?.subtle)
            throw new Error("Nearby chat requires a secure context and WebRTC support on this device.");
        close(current);
        const pc = new RTCPeerConnection({ iceServers: [], bundlePolicy: "max-bundle" });
        const audio = document.createElement("audio");
        document.body.appendChild(audio);
        const session = { pc, audio, messages: [] };
        current = session;
        session.pairingTimeout = setTimeout(() => close(session, "Pairing expired. Start a new pairing."), 5 * 60 * 1000);
        pc.onconnectionstatechange = () => {
            clearTimeout(session.reconnectTimeout);
            if (pc.connectionState === "failed" || pc.connectionState === "closed")
                close(session, "Couldn’t connect. Check that both devices are on the same network.");
            if (pc.connectionState === "disconnected")
                session.reconnectTimeout = setTimeout(() => close(session, "The local connection was interrupted."), 12000);
        };
        pc.ontrack = ({ track }) => {
            if (track.kind === "audio" && track === session.voice?.receiver.track) {
                session.remoteTrack = track;
                if (session.track) void playAudio(session);
            }
        };
        return session;
    }

    async function playAudio(session) {
        if (!session.track || !session.remoteTrack || !connected(session)) return;
        session.audio.srcObject ??= new MediaStream([session.remoteTrack]);
        try {
            await session.audio.play();
            session.playbackBlocked = false;
        } catch {
            session.playbackBlocked = Boolean(session.track);
        }
    }

    globalThis.egrooNearby = {
        async invite() {
            const session = start();
            try {
                session.voice = session.pc.addTransceiver("audio", { direction: "sendrecv" });
                session.remoteTrack = session.voice.receiver.track;
                attachChannel(session, session.pc.createDataChannel(label));
                session.offer = await makeCode(session, await session.pc.createOffer());
                return session.offer;
            } catch (error) {
                close(session);
                throw error;
            }
        },
        async reply(code) {
            const offer = decodeCode(code, "offer");
            const session = start();
            try {
                session.pc.ondatachannel = ({ channel }) => attachChannel(session, channel);
                await session.pc.setRemoteDescription(offer);
                session.voice = session.pc.getTransceivers().find(item => item.receiver.track.kind === "audio");
                if (!session.voice) throw new Error("The invitation does not support Nearby voice.");
                session.remoteTrack = session.voice.receiver.track;
                session.voice.direction = "sendrecv";
                const answer = await makeCode(session, await session.pc.createAnswer());
                await setVerificationCode(session, encodeCode(offer), answer);
                return answer;
            } catch (error) {
                close(session);
                throw error;
            }
        },
        async accept(code) {
            const session = current;
            if (!session?.offer || session.closed || session.verificationCode)
                throw new Error("Create a new invitation first.");
            const answer = decodeCode(code, "answer");
            await session.pc.setRemoteDescription(answer);
            await setVerificationCode(session, session.offer, encodeCode(answer));
        },
        confirm() {
            const session = current;
            if (!session?.verificationCode || session.closed) throw new Error("Finish exchanging pairing codes first.");
            session.confirmed = true;
            try {
                if (session.channel?.readyState === "open") session.sendConfirmation();
            } catch (error) {
                session.confirmed = false;
                throw error;
            }
            finishPairing(session);
        },
        send(text) {
            const session = requireConnected();
            if (typeof text !== "string" || !text.trim() || text.length > maxMessage)
                throw new Error("Enter a message of up to 8,000 characters.");
            sendFrame(session, { type: "message", text });
        },
        async joinVoice() {
            const session = requireConnected();
            if (session.track) return;
            if (!navigator.mediaDevices?.getUserMedia) throw new Error("Microphone access is unavailable on this device.");
            const stream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
                video: false
            });
            const track = stream.getAudioTracks()[0];
            try {
                if (!connected(session) || !track) throw new Error("Voice is no longer available for this connection.");
                track.enabled = false;
                await session.voice.sender.replaceTrack(track);
                if (!connected(session)) throw new Error("The local connection ended.");
                sendFrame(session, { type: "voice", active: true });
                session.track = track;
                updateVoiceSending(session);
                track.onended = () => {
                    if (session.track === track) void this.leaveVoice().catch(() => {});
                };
                await playAudio(session);
            } catch (error) {
                track?.stop();
                throw error;
            }
        },
        async leaveVoice() {
            const session = current;
            if (!session?.track) return;
            releaseVoice(session);
            await session.voice.sender.replaceTrack(null);
            if (connected(session)) sendFrame(session, { type: "voice", active: false });
        },
        toggleMute() {
            const session = requireConnected();
            if (!session.track) return;
            session.muted = !session.muted;
            updateVoiceSending(session);
        },
        playAudio() { return playAudio(requireConnected()); },
        reset() {
            close(current);
            current = undefined;
        },
        poll() {
            if (!current) return null;
            const messages = current.messages.splice(0, 32);
            return {
                connected: connected(current),
                wasConnected: Boolean(current.wasConnected),
                closed: Boolean(current.closed),
                reason: current.reason,
                verificationCode: current.verificationCode,
                confirmed: Boolean(current.confirmed),
                inVoice: Boolean(current.track),
                muted: Boolean(current.muted),
                peerVoice: Boolean(current.peerVoice),
                playbackBlocked: Boolean(current.playbackBlocked),
                messages,
                hasMoreMessages: current.messages.length > 0
            };
        }
    };
})();
