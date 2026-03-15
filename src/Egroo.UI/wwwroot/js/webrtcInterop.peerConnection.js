/**
 * WebRTC Interop — Peer Connection Management.
 * Handles peer lifecycle, ICE restart, disconnect recovery, and stats logging.
 */
Object.assign(window.webrtcInterop, {
    _getOrCreatePeer: function (peerId) {
        if (this.peers.has(peerId)) {
            return this.peers.get(peerId);
        }

        const pc = new RTCPeerConnection(this._buildPeerConnectionConfig());
        const peerData = {
            pc: pc,
            iceCandidateQueue: [],
            restartOfferInFlight: false,
            iceRestartAttempts: 0,
            disconnectTimer: null,
            selectedCandidatePairLogged: false,
            signalQueue: Promise.resolve()
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

    _withPeerSignalingQueue: function (peerData, fn) {
        const next = peerData.signalQueue
            .then(fn)
            .catch(err => {
                console.error("[WebRTC] Signaling queue error:", err);
                return null;
            });
        peerData.signalQueue = next;
        return next;
    },

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
    }
});
