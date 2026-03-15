/**
 * WebRTC Interop for Channel-Based Multi-Peer Voice Calling.
 * Manages one RTCPeerConnection per remote peer (mesh topology).
 *
 * This is the core module. Additional concerns are split into:
 *   - webrtcInterop.iceConfig.js        (ICE server configuration)
 *   - webrtcInterop.peerConnection.js   (peer lifecycle & recovery)
 *   - webrtcInterop.signaling.js        (SDP offer/answer/ICE exchange)
 *   - webrtcInterop.audioProcessing.js  (audio constraints & preferences)
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
            { urls: "stun:stun.cloudflare.com:3478" }
        ]
    },

    /**
     * Register the DotNet object reference for callbacks into Blazor.
     */
    registerDotNetObject: function (dotNetObject) {
        this.dotNetObject = dotNetObject;
        this._loadPersistedAudioProcessingPreferences();
        this._refreshAudioProcessingSupport();
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
     * Get the number of active peer connections.
     */
    getPeerCount: function () {
        return this.peers.size;
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
