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
    selfUserId: null,
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
        iceTransportPolicy: "all",
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

        const resolvedIceServers = normalizedServers.length > 0
            ? normalizedServers
            : this._getDefaultIceServers();
        const relayConfigured = resolvedIceServers.some(server => this._serverSupportsRelay(server));

        this.config = {
            ...this.config,
            iceServers: resolvedIceServers,
            iceTransportPolicy: this._shouldForceRelayTransport(relayConfigured) ? "relay" : "all"
        };

        console.log("[WebRTC] ICE servers configured:", this.config.iceServers.map(server => ({
            urls: server.urls,
            hasCredential: !!server.credential,
            hasUsername: !!server.username,
            supportsRelay: this._serverSupportsRelay(server)
        })));
        console.log("[WebRTC] ICE transport policy:", this.config.iceTransportPolicy);

        if (!this._hasRelayIceServer()) {
            const message = "[WebRTC] No TURN server configured. Calls may fail across NATs/firewalls outside local testing.";
            if (this._isProductionLikeHost()) {
                console.error(message);
            } else {
                console.warn(message);
            }
        }
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
    joinCall: async function (channelId, selfUserId) {
        this.channelId = channelId;
        this.selfUserId = selfUserId || null;
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

        // Guard against concurrent offer creation (async race)
        if (peerData.creatingOffer) {
            console.log("[WebRTC] Offer creation already in progress for peer", peerId);
            return null;
        }

        // If we already have a pending local offer, reuse it
        if (pc.signalingState === "have-local-offer" && pc.localDescription) {
            console.log("[WebRTC] Reusing existing local offer for peer", peerId);
            return pc.localDescription.sdp;
        }

        // If we already have a remote offer (glare handled elsewhere), skip
        if (pc.signalingState === "have-remote-offer") {
            console.log("[WebRTC] Skipping offer for peer", peerId, "- already processing their offer");
            return null;
        }

        peerData.creatingOffer = true;
        try {
            // Add local audio tracks
            this.localStream.getAudioTracks().forEach(track => {
                // Avoid duplicate tracks
                const senders = pc.getSenders();
                if (!senders.find(s => s.track === track)) {
                    pc.addTrack(track, this.localStream);
                }
            });

            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            console.log("[WebRTC] Created offer for peer", peerId);
            return offer.sdp;
        } catch (err) {
            console.error("[WebRTC] Error creating offer for peer", peerId, err);
            return null;
        } finally {
            peerData.creatingOffer = false;
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

        if (peerData.offerInFlight && peerData.activeOfferSdp === sdpOffer) {
            console.log("[WebRTC] Ignoring duplicate in-flight offer from peer", peerId);
            return null;
        }

        if (pc.localDescription?.type === "answer"
            && pc.remoteDescription?.type === "offer"
            && pc.remoteDescription.sdp === sdpOffer) {
            console.log("[WebRTC] Reusing existing answer for duplicate offer from peer", peerId);
            return pc.localDescription.sdp ?? null;
        }

        // Add local audio tracks
        this.localStream.getAudioTracks().forEach(track => {
            const senders = pc.getSenders();
            if (!senders.find(s => s.track === track)) {
                pc.addTrack(track, this.localStream);
            }
        });

        peerData.offerInFlight = true;
        peerData.activeOfferSdp = sdpOffer;

        try {
            // Perfect negotiation: if we have a pending local offer, determine polite/impolite
            if (pc.signalingState === "have-local-offer") {
                // The peer with the smaller ID is "polite" (will rollback).
                // The peer with the larger ID is "impolite" (keeps own offer, ignores incoming).
                const isPolite = this.selfUserId && peerId && (this.selfUserId < peerId);
                if (isPolite) {
                    console.log("[WebRTC] Glare: polite peer rolling back local offer for peer", peerId);
                    await pc.setLocalDescription({ type: "rollback" });
                } else {
                    console.log("[WebRTC] Glare: impolite peer ignoring incoming offer from peer", peerId);
                    peerData.offerInFlight = false;
                    return null;
                }
            }

            const isNewOffer = !pc.remoteDescription
                || pc.remoteDescription.type !== "offer"
                || pc.remoteDescription.sdp !== sdpOffer;

            if (isNewOffer) {
                await pc.setRemoteDescription(new RTCSessionDescription({ type: "offer", sdp: sdpOffer }));
            }

            // Flush queued ICE candidates
            await this._flushIceCandidates(peerId);

            if (pc.signalingState !== "have-remote-offer") {
                if (pc.localDescription?.type === "answer") {
                    console.log("[WebRTC] Offer from peer", peerId, "was already answered during re-entry.");
                    return pc.localDescription.sdp ?? null;
                }

                console.warn("[WebRTC] Skipping answer creation for peer", peerId, "because signaling state is", pc.signalingState);
                return null;
            }

            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            console.log("[WebRTC] Created answer for peer", peerId);
            return answer.sdp;
        } catch (err) {
            console.error("[WebRTC] Error handling offer from peer", peerId, err);

            if (pc.localDescription?.type === "answer"
                && pc.remoteDescription?.type === "offer"
                && pc.remoteDescription.sdp === sdpOffer) {
                console.log("[WebRTC] Returning existing answer after duplicate-offer error for peer", peerId);
                return pc.localDescription.sdp ?? null;
            }

            return null;
        } finally {
            if (peerData.activeOfferSdp === sdpOffer) {
                peerData.offerInFlight = false;
            }
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

        // Guard against concurrent answer application (async race)
        if (peerData.applyingAnswer) {
            console.log("[WebRTC] Answer application already in progress for peer", peerId);
            return;
        }

        // Can only apply an answer when we have a pending local offer
        if (peerData.pc.signalingState !== "have-local-offer") {
            console.log("[WebRTC] Ignoring answer from peer", peerId, "- signaling state:", peerData.pc.signalingState);
            return;
        }

        peerData.applyingAnswer = true;
        try {
            await peerData.pc.setRemoteDescription(new RTCSessionDescription({ type: "answer", sdp: sdpAnswer }));
            peerData.restartOfferInFlight = false;
            console.log("[WebRTC] Set remote answer for peer", peerId);
            // Flush queued ICE candidates
            await this._flushIceCandidates(peerId);
        } catch (err) {
            console.error("[WebRTC] Error setting answer for peer", peerId, err);
        } finally {
            peerData.applyingAnswer = false;
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
        // Collect peer IDs first to avoid mutating the map during iteration
        const peerIds = Array.from(this.peers.keys());
        for (const peerId of peerIds) {
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
        this.selfUserId = null;
        this.isMuted = false;
        console.log("[WebRTC] Left call.");
    },

    /**
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

        const pc = new RTCPeerConnection(this._buildPeerConnectionConfig());
        const peerData = {
            pc: pc,
            iceCandidateQueue: [],
            offerInFlight: false,
            activeOfferSdp: null,
            restartOfferInFlight: false,
            iceRestartAttempts: 0,
            disconnectTimer: null,
            selectedCandidatePairLogged: false,
            creatingOffer: false,
            applyingAnswer: false
        };

        pc.onicecandidate = (event) => {
            if (event.candidate && this.dotNetObject && this.channelId) {
                const summary = this._parseIceCandidate(event.candidate.candidate);
                console.log("[WebRTC] Local ICE candidate for peer", peerId, summary);
                this.dotNetObject.invokeMethodAsync(
                    'OnIceCandidateGenerated',
                    peerId,
                    JSON.stringify(event.candidate)
                );
            }
        };

        pc.onicecandidateerror = (event) => {
            console.error("[WebRTC] ICE candidate error for peer", peerId, {
                address: event.address,
                port: event.port,
                url: event.url,
                errorCode: event.errorCode,
                errorText: event.errorText
            });
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
            audioEl.play().then(() => {
                console.log("[WebRTC] Audio playing for peer", peerId);
            }).catch(err => {
                console.warn("[WebRTC] Autoplay blocked for peer", peerId, err);
                // Retry audio playback on next user interaction
                const resumeAudio = () => {
                    audioEl.play().then(() => {
                        console.log("[WebRTC] Audio resumed for peer", peerId, "after user interaction");
                    }).catch(() => {});
                };
                document.addEventListener("click", resumeAudio, { once: true });
                document.addEventListener("keydown", resumeAudio, { once: true });
            });
        };

        pc.oniceconnectionstatechange = () => {
            console.log("[WebRTC] Peer", peerId, "ICE state:", pc.iceConnectionState);
            if (pc.iceConnectionState === "connected" || pc.iceConnectionState === "completed") {
                peerData.iceRestartAttempts = 0;
                peerData.restartOfferInFlight = false;
                this._clearDisconnectTimer(peerData);
                this._logSelectedCandidatePair(peerId, peerData);
            } else if (pc.iceConnectionState === "failed") {
                this._clearDisconnectTimer(peerData);
                console.warn("[WebRTC] Peer", peerId, "ICE connection failed. Attempting ICE restart renegotiation...");
                this._restartIceForPeer(peerId, "failed");
            } else if (pc.iceConnectionState === "disconnected") {
                console.warn("[WebRTC] Peer", peerId, "ICE temporarily disconnected (may recover).");
                this._scheduleDisconnectRecovery(peerId, peerData);
            } else if (pc.iceConnectionState === "closed") {
                this._clearDisconnectTimer(peerData);
            }
        };

        pc.onconnectionstatechange = () => {
            console.log("[WebRTC] Peer", peerId, "connection state:", pc.connectionState);
            if (pc.connectionState === "connected") {
                this._logSelectedCandidatePair(peerId, peerData);
            } else if (pc.connectionState === "failed") {
                this._restartIceForPeer(peerId, "connection-failed");
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

    _buildPeerConnectionConfig: function () {
        return {
            ...this.config,
            iceServers: Array.isArray(this.config.iceServers)
                ? this.config.iceServers.map(server => ({ ...server }))
                : []
        };
    },

    _isProductionLikeHost: function () {
        const hostname = window.location && typeof window.location.hostname === "string"
            ? window.location.hostname.toLowerCase()
            : "";

        if (!hostname) {
            return false;
        }

        return hostname !== "localhost"
            && hostname !== "127.0.0.1"
            && hostname !== "::1"
            && !hostname.endsWith(".local");
    },

    _shouldForceRelayTransport: function (relayConfigured) {
        return relayConfigured && this._isProductionLikeHost();
    },

    _serverSupportsRelay: function (server) {
        if (!server) {
            return false;
        }

        const urls = Array.isArray(server.urls)
            ? server.urls
            : typeof server.urls === "string"
                ? [server.urls]
                : [];

        return urls.some(url => typeof url === "string" && /^turns?:/i.test(url));
    },

    _hasRelayIceServer: function () {
        return Array.isArray(this.config.iceServers)
            && this.config.iceServers.some(server => this._serverSupportsRelay(server));
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

    _clearDisconnectTimer: function (peerData) {
        if (peerData && peerData.disconnectTimer) {
            clearTimeout(peerData.disconnectTimer);
            peerData.disconnectTimer = null;
        }
    },

    _scheduleDisconnectRecovery: function (peerId, peerData) {
        if (!peerData || peerData.disconnectTimer) {
            return;
        }

        peerData.disconnectTimer = setTimeout(() => {
            peerData.disconnectTimer = null;

            if (!this.peers.has(peerId)) {
                return;
            }

            const currentPeerData = this.peers.get(peerId);
            if (!currentPeerData) {
                return;
            }

            const state = currentPeerData.pc.iceConnectionState;
            if (state === "disconnected" || state === "failed") {
                console.warn("[WebRTC] Peer", peerId, "did not recover from disconnect. Attempting ICE restart renegotiation...");
                this._restartIceForPeer(peerId, "disconnected");
            }
        }, 3000);
    },

    _restartIceForPeer: async function (peerId, reason) {
        const peerData = this.peers.get(peerId);
        if (!peerData || !peerData.pc || !this.dotNetObject || !this.channelId) {
            return;
        }

        if (peerData.restartOfferInFlight) {
            console.log("[WebRTC] ICE restart already in flight for peer", peerId);
            return;
        }

        if (peerData.pc.signalingState !== "stable") {
            console.warn("[WebRTC] Cannot ICE-restart peer", peerId, "while signaling state is", peerData.pc.signalingState);
            return;
        }

        if (peerData.iceRestartAttempts >= 2) {
            console.error("[WebRTC] Peer", peerId, "exceeded ICE restart attempts. Removing peer.");
            this.dotNetObject.invokeMethodAsync('OnPeerDisconnected', peerId);
            return;
        }

        try {
            peerData.restartOfferInFlight = true;
            peerData.iceRestartAttempts += 1;

            const offer = await peerData.pc.createOffer({ iceRestart: true });
            await peerData.pc.setLocalDescription(offer);

            console.log("[WebRTC] Created ICE restart offer for peer", peerId, "| Attempt:", peerData.iceRestartAttempts, "| Reason:", reason);
            await this.dotNetObject.invokeMethodAsync('OnOfferGenerated', peerId, offer.sdp);
        } catch (err) {
            peerData.restartOfferInFlight = false;
            console.error("[WebRTC] Error creating ICE restart offer for peer", peerId, err);
        }
    },

    _parseIceCandidate: function (candidateLine) {
        if (typeof candidateLine !== "string") {
            return { type: "unknown" };
        }

        const parts = candidateLine.trim().split(/\s+/);
        const typeIndex = parts.indexOf("typ");
        const protocol = parts.length > 2 ? parts[2].toLowerCase() : "unknown";
        const candidateType = typeIndex >= 0 && parts.length > typeIndex + 1
            ? parts[typeIndex + 1].toLowerCase()
            : "unknown";

        return {
            protocol: protocol,
            type: candidateType,
            candidate: candidateLine
        };
    },

    _logSelectedCandidatePair: function (peerId, peerData) {
        if (!peerData || peerData.selectedCandidatePairLogged || !peerData.pc || typeof peerData.pc.getStats !== "function") {
            return;
        }

        peerData.selectedCandidatePairLogged = true;

        peerData.pc.getStats(null)
            .then(stats => {
                let selectedPair = null;
                let localCandidate = null;
                let remoteCandidate = null;

                stats.forEach(report => {
                    if (!selectedPair && report.type === "transport" && report.selectedCandidatePairId) {
                        selectedPair = stats.get(report.selectedCandidatePairId) || null;
                    }

                    if (!selectedPair && report.type === "candidate-pair" && report.nominated && report.state === "succeeded") {
                        selectedPair = report;
                    }
                });

                if (!selectedPair) {
                    console.warn("[WebRTC] No selected candidate pair found for peer", peerId);
                    return;
                }

                if (selectedPair.localCandidateId) {
                    localCandidate = stats.get(selectedPair.localCandidateId) || null;
                }

                if (selectedPair.remoteCandidateId) {
                    remoteCandidate = stats.get(selectedPair.remoteCandidateId) || null;
                }

                console.log("[WebRTC] Selected candidate pair for peer", peerId, {
                    local: localCandidate ? {
                        candidateType: localCandidate.candidateType,
                        protocol: localCandidate.protocol,
                        relayProtocol: localCandidate.relayProtocol,
                        url: localCandidate.url
                    } : null,
                    remote: remoteCandidate ? {
                        candidateType: remoteCandidate.candidateType,
                        protocol: remoteCandidate.protocol,
                        relayProtocol: remoteCandidate.relayProtocol,
                        url: remoteCandidate.url
                    } : null,
                    state: selectedPair.state,
                    nominated: selectedPair.nominated,
                    currentRoundTripTime: selectedPair.currentRoundTripTime
                });
            })
            .catch(err => {
                peerData.selectedCandidatePairLogged = false;
                console.error("[WebRTC] Error retrieving selected candidate pair for peer", peerId, err);
            });
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
