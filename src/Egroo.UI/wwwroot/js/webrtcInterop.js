/**
 * WebRTC Interop for Channel-Based Multi-Peer Voice Calling.
 * Manages one RTCPeerConnection per remote peer (mesh topology).
 */
window.webrtcInterop = {
    // Map of peerId (GUID string) -> { pc: RTCPeerConnection, iceCandidateQueue: [] }
    peers: new Map(),
    localStream: null,
    dotNetObject: null,
    channelId: null,
    statsInterval: null,
    isMuted: false,
    audioProcessingStorageKey: "egroo.voice.audioProcessing",
    remoteAudioContainerId: "egroo-remote-audio-container",
    isNoiseSuppressionEnabled: true,
    isNoiseSuppressionSupported: false,
    isEchoCancellationEnabled: true,
    isEchoCancellationSupported: false,
    isAutoGainControlEnabled: true,
    isAutoGainControlSupported: false,

    config: {
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" },
            { urls: "stun:stun1.l.google.com:19302" }
        ]
    },

    configureIceServers: function (iceServers) {
        const normalizedServers = Array.isArray(iceServers)
            ? iceServers
                .map(server => this._normalizeIceServer(server))
                .filter(server => server !== null)
            : [];

        this.config = {
            ...this.config,
            iceServers: normalizedServers.length > 0
                ? normalizedServers
                : this._getDefaultIceServers()
        };

        console.log("[WebRTC] ICE servers configured:", this.config.iceServers.map(server => ({ urls: server.urls })));
    },

    /**
     * Register the DotNet object reference for callbacks into Blazor.
     */
    registerDotNetObject: function (dotNetObject) {
        this.dotNetObject = dotNetObject;
        this._loadPersistedAudioProcessingPreferences();
        this._refreshAudioProcessingSupport();
        console.log("[WebRTC] DotNetObjectReference registered.");
    },

    /**
     * Acquire local audio stream (microphone).
     * Returns true on success.
     */
    acquireLocalStream: async function () {
        if (this.localStream) return true;
        try {
            this._loadPersistedAudioProcessingPreferences();
            this.localStream = await navigator.mediaDevices.getUserMedia(this._buildUserMediaConstraints());
            this._syncAudioProcessingFromTrack(this._getPrimaryAudioTrack());
            this._persistAudioProcessingPreferences();
            console.log("[WebRTC] Local audio stream acquired:", this.localStream.getAudioTracks().length, "audio tracks");
            return true;
        } catch (err) {
            console.error("[WebRTC] Error acquiring microphone:", err);
            return false;
        }
    },

    /**
     * Start participating in a channel call. Acquires mic and sets channel context.
     * Returns true if microphone was successfully acquired.
     */
    joinCall: async function (channelId) {
        this.channelId = channelId;
        this._loadPersistedAudioProcessingPreferences();
        const acquired = await this.acquireLocalStream();
        if (!acquired) return false;

        // Start stats polling
        if (this.statsInterval) clearInterval(this.statsInterval);
        this.statsInterval = setInterval(() => this.pollAudioStats(), 10000);

        return true;
    },

    /**
     * Create an RTCPeerConnection for a specific peer, create an SDP offer, and return it.
     * The caller side initiates the offer.
     */
    createOfferForPeer: async function (peerId) {
        if (!this.localStream) {
            console.error("[WebRTC] No local stream. Call joinCall first.");
            return null;
        }

        const peerData = this._getOrCreatePeer(peerId);
        const pc = peerData.pc;

        // Add local audio tracks
        this.localStream.getAudioTracks().forEach(track => {
            // Avoid duplicate tracks
            const senders = pc.getSenders();
            if (!senders.find(s => s.track === track)) {
                pc.addTrack(track, this.localStream);
            }
        });

        try {
            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            console.log("[WebRTC] Created offer for peer", peerId);
            return offer.sdp;
        } catch (err) {
            console.error("[WebRTC] Error creating offer for peer", peerId, err);
            return null;
        }
    },

    /**
     * Handle an incoming SDP offer from a peer. Creates answer SDP and returns it.
     */
    handleOfferFromPeer: async function (peerId, sdpOffer) {
        if (!this.localStream) {
            console.error("[WebRTC] No local stream. Call joinCall first.");
            return null;
        }

        const peerData = this._getOrCreatePeer(peerId);
        const pc = peerData.pc;

        // Add local audio tracks
        this.localStream.getAudioTracks().forEach(track => {
            const senders = pc.getSenders();
            if (!senders.find(s => s.track === track)) {
                pc.addTrack(track, this.localStream);
            }
        });

        try {
            await pc.setRemoteDescription(new RTCSessionDescription({ type: "offer", sdp: sdpOffer }));

            // Flush queued ICE candidates
            await this._flushIceCandidates(peerId);

            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            console.log("[WebRTC] Created answer for peer", peerId);
            return answer.sdp;
        } catch (err) {
            console.error("[WebRTC] Error handling offer from peer", peerId, err);
            return null;
        }
    },

    /**
     * Handle an incoming SDP answer from a peer.
     */
    handleAnswerFromPeer: async function (peerId, sdpAnswer) {
        const peerData = this.peers.get(peerId);
        if (!peerData) {
            console.warn("[WebRTC] No peer connection for", peerId, "to set answer on.");
            return;
        }

        try {
            await peerData.pc.setRemoteDescription(new RTCSessionDescription({ type: "answer", sdp: sdpAnswer }));
            console.log("[WebRTC] Set remote answer for peer", peerId);
            // Flush queued ICE candidates
            await this._flushIceCandidates(peerId);
        } catch (err) {
            console.error("[WebRTC] Error setting answer for peer", peerId, err);
        }
    },

    /**
     * Add an ICE candidate received from a specific peer.
     */
    addIceCandidateForPeer: async function (peerId, candidateJson) {
        const peerData = this.peers.get(peerId);

        let candidate;
        try {
            const parsed = JSON.parse(candidateJson);
            candidate = new RTCIceCandidate(parsed);
        } catch (err) {
            console.warn("[WebRTC] Error parsing ICE candidate:", err);
            return;
        }

        if (!peerData) {
            // Peer connection doesn't exist yet, queue the candidate
            console.log("[WebRTC] Queuing ICE candidate for peer", peerId);
            const newPeerData = this._getOrCreatePeer(peerId);
            newPeerData.iceCandidateQueue.push(candidate);
            return;
        }

        if (!peerData.pc.remoteDescription) {
            // Remote description not set yet, queue
            peerData.iceCandidateQueue.push(candidate);
            return;
        }

        try {
            await peerData.pc.addIceCandidate(candidate);
        } catch (err) {
            console.error("[WebRTC] Error adding ICE candidate for peer", peerId, err);
        }
    },

    /**
     * Remove a specific peer connection (when a user leaves the call).
     */
    removePeer: function (peerId) {
        const peerData = this.peers.get(peerId);
        if (!peerData) return;

        peerData.pc.onicecandidate = null;
        peerData.pc.ontrack = null;
        peerData.pc.oniceconnectionstatechange = null;
        peerData.pc.onsignalingstatechange = null;
        peerData.pc.close();
        this.peers.delete(peerId);

        // Remove the audio element for this peer
        const audioEl = document.getElementById("remoteAudio-" + peerId);
        if (audioEl) {
            audioEl.srcObject = null;
            audioEl.remove();
        }

        console.log("[WebRTC] Removed peer", peerId, "| Remaining peers:", this.peers.size);
    },

    /**
     * Leave the call entirely: close all peer connections and release microphone.
     */
    leaveCall: function () {
        // Close all peer connections
        for (const [peerId] of this.peers) {
            this.removePeer(peerId);
        }
        this.peers.clear();

        // Stop local stream
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop());
            this.localStream = null;
            console.log("[WebRTC] Local stream stopped.");
        }

        // Clear stats interval
        if (this.statsInterval) {
            clearInterval(this.statsInterval);
            this.statsInterval = null;
        }

        // Remove all dynamically created remote audio elements
        const container = document.getElementById(this.remoteAudioContainerId);
        if (container) {
            container.innerHTML = "";
        }

        this.channelId = null;
        this.isMuted = false;
        console.log("[WebRTC] Left call.");
    },

    /**
     * Toggle microphone mute state.
     * Returns true if now muted, false if now unmuted.
     */
    toggleMute: function () {
        if (!this.localStream) return this.isMuted;
        this.localStream.getAudioTracks().forEach(track => {
            track.enabled = !track.enabled;
        });
        this.isMuted = !this.isMuted;
        console.log("[WebRTC] Muted:", this.isMuted);
        return this.isMuted;
    },

    getAudioProcessingState: function () {
        this._loadPersistedAudioProcessingPreferences();
        this._refreshAudioProcessingSupport();
        this._syncAudioProcessingFromTrack(this._getPrimaryAudioTrack());
        this._persistAudioProcessingPreferences();
        return this._createAudioProcessingState(true);
    },

    setAudioProcessingSetting: async function (settingName, enabled) {
        const enabledMap = {
            noiseSuppression: "isNoiseSuppressionEnabled",
            echoCancellation: "isEchoCancellationEnabled",
            autoGainControl: "isAutoGainControlEnabled"
        };
        const supportedMap = {
            noiseSuppression: "isNoiseSuppressionSupported",
            echoCancellation: "isEchoCancellationSupported",
            autoGainControl: "isAutoGainControlSupported"
        };

        const enabledProperty = enabledMap[settingName];
        const supportedProperty = supportedMap[settingName];

        if (!enabledProperty || !supportedProperty) {
            return this._createAudioProcessingState(false);
        }

        this._loadPersistedAudioProcessingPreferences();
        this._refreshAudioProcessingSupport();

        const previousEnabled = this[enabledProperty];
        this[enabledProperty] = !!enabled;
        this._persistAudioProcessingPreferences();

        if (!this[supportedProperty]) {
            return this._createAudioProcessingState(false);
        }

        try {
            const track = await this._recreateLocalAudioTrack();
            const wasApplied = this._verifyAudioProcessingSetting(track, settingName, this[enabledProperty]);
            this._persistAudioProcessingPreferences();
            return this._createAudioProcessingState(wasApplied);
        } catch (err) {
            this[enabledProperty] = previousEnabled;
            this._persistAudioProcessingPreferences();
            console.error("[WebRTC] Error applying audio processing setting:", settingName, err);
            return this._createAudioProcessingState(false);
        }
    },

    /**
     * Get the number of active peer connections.
     */
    getPeerCount: function () {
        return this.peers.size;
    },

    // ---- Internal Helpers ----

    _getOrCreatePeer: function (peerId) {
        if (this.peers.has(peerId)) {
            return this.peers.get(peerId);
        }

        const pc = new RTCPeerConnection(this.config);
        const peerData = { pc: pc, iceCandidateQueue: [] };

        pc.onicecandidate = (event) => {
            if (event.candidate && this.dotNetObject && this.channelId) {
                this.dotNetObject.invokeMethodAsync(
                    'OnIceCandidateGenerated',
                    peerId,
                    JSON.stringify(event.candidate)
                );
            }
        };

        pc.ontrack = (event) => {
            console.log("[WebRTC] Remote track received from peer", peerId);
            const container = this._ensureRemoteAudioContainer();

            let audioEl = document.getElementById("remoteAudio-" + peerId);
            if (!audioEl) {
                audioEl = document.createElement("audio");
                audioEl.id = "remoteAudio-" + peerId;
                audioEl.autoplay = true;
                audioEl.playsInline = true;
                container.appendChild(audioEl);
            }

            const stream = (event.streams && event.streams.length > 0)
                ? event.streams[0]
                : new MediaStream([event.track]);
            audioEl.srcObject = stream;
            audioEl.muted = false;
            audioEl.volume = 1.0;
            audioEl.play().catch(err => console.error("[WebRTC] Error playing remote audio from peer", peerId, err));
        };

        pc.oniceconnectionstatechange = () => {
            console.log("[WebRTC] Peer", peerId, "ICE state:", pc.iceConnectionState);
            if (pc.iceConnectionState === "disconnected" || pc.iceConnectionState === "failed") {
                console.warn("[WebRTC] Peer", peerId, "connection lost.");
                if (this.dotNetObject) {
                    this.dotNetObject.invokeMethodAsync('OnPeerDisconnected', peerId);
                }
            }
        };

        pc.onsignalingstatechange = () => {
            console.log("[WebRTC] Peer", peerId, "signaling state:", pc.signalingState);
        };

        this.peers.set(peerId, peerData);
        console.log("[WebRTC] Created peer connection for", peerId, "| Total peers:", this.peers.size);
        return peerData;
    },

    _buildUserMediaConstraints: function () {
        this._refreshAudioProcessingSupport();
        const audioConstraints = this._buildTrackConstraints();

        if (!audioConstraints) {
            return { audio: true, video: false };
        }

        return {
            audio: audioConstraints,
            video: false
        };
    },

    _getDefaultIceServers: function () {
        return [
            { urls: "stun:stun.l.google.com:19302" },
            { urls: "stun:stun1.l.google.com:19302" }
        ];
    },

    _normalizeIceServer: function (server) {
        if (!server) {
            return null;
        }

        const urls = Array.isArray(server.urls)
            ? server.urls
            : Array.isArray(server.Urls)
                ? server.Urls
                : typeof server.urls === "string"
                    ? [server.urls]
                    : typeof server.Urls === "string"
                        ? [server.Urls]
                        : [];

        const normalizedUrls = urls
            .filter(url => typeof url === "string" && url.trim().length > 0)
            .map(url => url.trim());

        if (normalizedUrls.length === 0) {
            return null;
        }

        const normalizedServer = {
            urls: normalizedUrls.length === 1 ? normalizedUrls[0] : normalizedUrls
        };

        const username = typeof server.username === "string"
            ? server.username.trim()
            : typeof server.Username === "string"
                ? server.Username.trim()
                : "";

        const credential = typeof server.credential === "string"
            ? server.credential.trim()
            : typeof server.Credential === "string"
                ? server.Credential.trim()
                : "";

        if (username.length > 0) {
            normalizedServer.username = username;
        }

        if (credential.length > 0) {
            normalizedServer.credential = credential;
        }

        return normalizedServer;
    },

    _buildTrackConstraints: function () {
        const constraints = {};

        if (this.isNoiseSuppressionSupported) {
            constraints.noiseSuppression = this.isNoiseSuppressionEnabled;
        }

        if (this.isEchoCancellationSupported) {
            constraints.echoCancellation = this.isEchoCancellationEnabled;
        }

        if (this.isAutoGainControlSupported) {
            constraints.autoGainControl = this.isAutoGainControlEnabled;
        }

        return Object.keys(constraints).length > 0 ? constraints : null;
    },

    _getPrimaryAudioTrack: function () {
        if (!this.localStream) {
            return null;
        }

        const audioTracks = this.localStream.getAudioTracks();
        return audioTracks.length > 0 ? audioTracks[0] : null;
    },

    _ensureRemoteAudioContainer: function () {
        let container = document.getElementById(this.remoteAudioContainerId);
        if (!container) {
            container = document.createElement("div");
            container.id = this.remoteAudioContainerId;
            container.style.display = "none";
            document.body.appendChild(container);
            return container;
        }

        if (container.parentElement !== document.body) {
            document.body.appendChild(container);
        }

        return container;
    },

    _refreshAudioProcessingSupport: function () {
        const supportedConstraints = navigator.mediaDevices && typeof navigator.mediaDevices.getSupportedConstraints === "function"
            ? navigator.mediaDevices.getSupportedConstraints()
            : {};

        this.isNoiseSuppressionSupported = !!supportedConstraints.noiseSuppression;
        this.isEchoCancellationSupported = !!supportedConstraints.echoCancellation;
        this.isAutoGainControlSupported = !!supportedConstraints.autoGainControl;
        return supportedConstraints;
    },

    _syncAudioProcessingFromTrack: function (track) {
        if (!track || typeof track.getSettings !== "function") {
            return;
        }

        const settings = track.getSettings();
        if (typeof settings.noiseSuppression === "boolean") {
            this.isNoiseSuppressionEnabled = settings.noiseSuppression;
        }
        if (typeof settings.echoCancellation === "boolean") {
            this.isEchoCancellationEnabled = settings.echoCancellation;
        }
        if (typeof settings.autoGainControl === "boolean") {
            this.isAutoGainControlEnabled = settings.autoGainControl;
        }
    },

    _recreateLocalAudioTrack: async function () {
        const previousStream = this.localStream;
        const previousTrack = this._getPrimaryAudioTrack();
        const newStream = await navigator.mediaDevices.getUserMedia(this._buildUserMediaConstraints());
        const newTrack = newStream.getAudioTracks()[0];

        if (!newTrack) {
            newStream.getTracks().forEach(track => track.stop());
            throw new Error("No audio track was returned when recreating the microphone stream.");
        }

        newTrack.enabled = !this.isMuted;

        const replaceOperations = [];
        for (const [, peerData] of this.peers) {
            const audioSenders = peerData.pc.getSenders().filter(sender => sender.track && sender.track.kind === "audio");

            if (audioSenders.length === 0) {
                peerData.pc.addTrack(newTrack, newStream);
                continue;
            }

            for (const sender of audioSenders) {
                replaceOperations.push(sender.replaceTrack(newTrack));
            }
        }

        await Promise.all(replaceOperations);

        this.localStream = newStream;
        this._syncAudioProcessingFromTrack(newTrack);

        if (previousStream) {
            previousStream.getTracks().forEach(track => {
                if (track !== newTrack) {
                    track.stop();
                }
            });
        }

        if (previousTrack && previousTrack !== newTrack) {
            previousTrack.stop();
        }

        return newTrack;
    },

    _verifyAudioProcessingSetting: function (track, settingName, expectedValue) {
        if (!track || typeof track.getSettings !== "function") {
            return false;
        }

        const settings = track.getSettings();
        if (typeof settings[settingName] !== "boolean") {
            return false;
        }

        return settings[settingName] === expectedValue;
    },

    _createAudioProcessingState: function (wasApplied) {
        return {
            isNoiseSuppressionSupported: this.isNoiseSuppressionSupported,
            isNoiseSuppressionEnabled: this.isNoiseSuppressionEnabled,
            isEchoCancellationSupported: this.isEchoCancellationSupported,
            isEchoCancellationEnabled: this.isEchoCancellationEnabled,
            isAutoGainControlSupported: this.isAutoGainControlSupported,
            isAutoGainControlEnabled: this.isAutoGainControlEnabled,
            wasApplied: wasApplied
        };
    },

    _loadPersistedAudioProcessingPreferences: function () {
        try {
            const raw = localStorage.getItem(this.audioProcessingStorageKey);
            if (!raw) {
                return;
            }

            const parsed = JSON.parse(raw);
            if (typeof parsed.isNoiseSuppressionEnabled === "boolean") {
                this.isNoiseSuppressionEnabled = parsed.isNoiseSuppressionEnabled;
            }
            if (typeof parsed.isEchoCancellationEnabled === "boolean") {
                this.isEchoCancellationEnabled = parsed.isEchoCancellationEnabled;
            }
            if (typeof parsed.isAutoGainControlEnabled === "boolean") {
                this.isAutoGainControlEnabled = parsed.isAutoGainControlEnabled;
            }
        } catch (err) {
            console.warn("[WebRTC] Error loading persisted audio processing preferences:", err);
        }
    },

    _persistAudioProcessingPreferences: function () {
        try {
            localStorage.setItem(this.audioProcessingStorageKey, JSON.stringify({
                isNoiseSuppressionEnabled: this.isNoiseSuppressionEnabled,
                isEchoCancellationEnabled: this.isEchoCancellationEnabled,
                isAutoGainControlEnabled: this.isAutoGainControlEnabled
            }));
        } catch (err) {
            console.warn("[WebRTC] Error persisting audio processing preferences:", err);
        }
    },

    _flushIceCandidates: async function (peerId) {
        const peerData = this.peers.get(peerId);
        if (!peerData || !peerData.pc.remoteDescription) return;

        while (peerData.iceCandidateQueue.length > 0) {
            const candidate = peerData.iceCandidateQueue.shift();
            try {
                await peerData.pc.addIceCandidate(candidate);
            } catch (err) {
                console.error("[WebRTC] Error flushing ICE candidate for peer", peerId, err);
            }
        }
    },

    pollAudioStats: function () {
        for (const [peerId, peerData] of this.peers) {
            peerData.pc.getStats(null)
                .then(stats => {
                    stats.forEach(report => {
                        if (report.type === "inbound-rtp" && report.kind === "audio") {
                            console.log("[WebRTC] Inbound audio stats from peer", peerId, ":", {
                                packetsReceived: report.packetsReceived,
                                bytesReceived: report.bytesReceived,
                                packetsLost: report.packetsLost
                            });
                        }
                    });
                })
                .catch(err => console.error("[WebRTC] Error getting stats for peer", peerId, err));
        }
    }
};
