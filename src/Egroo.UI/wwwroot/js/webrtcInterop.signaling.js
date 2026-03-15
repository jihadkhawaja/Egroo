/**
 * WebRTC Interop — Signaling (SDP Offer/Answer/ICE Candidate Exchange).
 * Handles creating offers, processing answers, and ICE candidate relay.
 */
Object.assign(window.webrtcInterop, {
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
        return this._withPeerSignalingQueue(peerData, async () => {
            const pc = peerData.pc;

            // If we already have a pending local offer, reuse it
            if (pc.signalingState === "have-local-offer" && pc.localDescription) {
                console.log("[WebRTC] Reusing existing local offer for peer", peerId);
                return pc.localDescription.sdp;
            }

            // If we already have a remote offer, skip (the other side is the offerer)
            if (pc.signalingState === "have-remote-offer") {
                console.log("[WebRTC] Skipping offer for peer", peerId, "- already processing their offer");
                return null;
            }

            // Add local audio tracks
            this.localStream.getAudioTracks().forEach(track => {
                const senders = pc.getSenders();
                if (!senders.find(s => s.track === track)) {
                    pc.addTrack(track, this.localStream);
                }
            });

            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            console.log("[WebRTC] Created offer for peer", peerId);
            return offer.sdp;
        });
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
        return this._withPeerSignalingQueue(peerData, async () => {
            const pc = peerData.pc;

            // Add local audio tracks
            this.localStream.getAudioTracks().forEach(track => {
                const senders = pc.getSenders();
                if (!senders.find(s => s.track === track)) {
                    pc.addTrack(track, this.localStream);
                }
            });

            // Glare: if we have a pending local offer, use perfect negotiation
            if (pc.signalingState === "have-local-offer") {
                const isPolite = this.selfUserId && peerId && (this.selfUserId < peerId);
                if (isPolite) {
                    console.log("[WebRTC] Glare: polite peer rolling back local offer for peer", peerId);
                    await pc.setLocalDescription({ type: "rollback" });
                } else {
                    console.log("[WebRTC] Glare: impolite peer ignoring incoming offer from peer", peerId);
                    return null;
                }
            }

            await pc.setRemoteDescription(new RTCSessionDescription({ type: "offer", sdp: sdpOffer }));
            await this._flushIceCandidates(peerId);
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);
            console.log("[WebRTC] Created answer for peer", peerId);
            return answer.sdp;
        });
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

        await this._withPeerSignalingQueue(peerData, async () => {
            const pc = peerData.pc;

            // Can only apply an answer when we have a pending local offer
            if (pc.signalingState !== "have-local-offer") {
                console.log("[WebRTC] Ignoring answer from peer", peerId, "- signaling state:", pc.signalingState);
                return;
            }

            await pc.setRemoteDescription(new RTCSessionDescription({ type: "answer", sdp: sdpAnswer }));
            peerData.restartOfferInFlight = false;
            console.log("[WebRTC] Set remote answer for peer", peerId);
            await this._flushIceCandidates(peerId);
        });
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
    }
});
