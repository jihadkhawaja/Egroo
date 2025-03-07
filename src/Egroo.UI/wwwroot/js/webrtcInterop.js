window.webrtcInterop = {
    pc: null,
    localStream: null,
    iceCandidatesQueue: [], // To store ICE candidates until DotNetObjectReference is registered
    statsInterval: null,    // Interval ID for polling stats
    dotNetObject: null,

    registerSendIceCandidateToPeer: function (dotNetObject) {
        this.dotNetObject = dotNetObject;
        console.log("📡 DotNetObjectReference registered for SendIceCandidateToPeer");

        // Send any previously queued ICE candidates
        this.iceCandidatesQueue.forEach(candidate => {
            this.dotNetObject.invokeMethodAsync('SendIceCandidate', JSON.stringify(candidate));
        });
        this.iceCandidatesQueue = []; // Clear the queue
    },

    pollAudioStats: function () {
        if (!this.pc) return;
        this.pc.getStats(null)
            .then(stats => {
                stats.forEach(report => {
                    if (report.type === "outbound-rtp" && report.kind === "audio") {
                        console.log("Audio Outbound Stats:", report);
                    } else if (report.type === "inbound-rtp" && report.kind === "audio") {
                        console.log("Audio Inbound Stats:", report);
                    }
                });
            })
            .catch(err => console.error("❌ Error getting stats:", err));
    },

    // Start a call as the caller: returns the SDP offer string.
    startCall: async function () {
        if (this.pc) {
            this.pc.close();
        }

        // Use simple constraints for testing.
        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            console.log("🎙️ Local stream acquired", this.localStream);
            console.log("Audio tracks:", this.localStream.getAudioTracks());
        } catch (err) {
            console.error("❌ Error getting local stream:", err);
            return;
        }

        // (Optional) For debugging, attach the local stream so you can hear your own mic.
        const localAudio = document.getElementById("localAudio");
        if (localAudio) {
            localAudio.srcObject = this.localStream;
            localAudio.play().catch(err => console.error("Error playing local audio:", err));
        }

        const config = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };
        this.pc = new RTCPeerConnection(config);

        if (this.localStream && this.localStream.getAudioTracks().length > 0) {
            let audioTrack = this.localStream.getAudioTracks()[0];
            console.log("Audio track details:", {
                enabled: audioTrack.enabled,
                readyState: audioTrack.readyState,
                label: audioTrack.label
            });
            audioTrack.enabled = true;
            this.pc.addTrack(audioTrack, this.localStream);
            console.log("✅ Added local audio track via addTrack.");
        } else {
            console.warn("⚠️ No local audio track available to add.");
        }

        this.pc.onsignalingstatechange = () => {
            console.log("Signaling state:", this.pc.signalingState);
        };
        this.pc.oniceconnectionstatechange = () => {
            console.log("ICE connection state:", this.pc.iceConnectionState);
        };

        this.pc.onicecandidate = event => {
            if (event.candidate) {
                console.log("📡 Local ICE candidate:", event.candidate.candidate);
                if (this.dotNetObject) {
                    this.dotNetObject.invokeMethodAsync('SendIceCandidate', JSON.stringify(event.candidate));
                } else {
                    console.log("❌ DotNetObjectReference not registered, queuing ICE candidate");
                    this.iceCandidatesQueue.push(event.candidate);
                }
            }
        };

        this.pc.ontrack = event => {
            console.log("🎧 Remote track received:", event);
            const remoteAudio = document.getElementById("remoteAudio");
            if (!remoteAudio) {
                console.error("Remote audio element not found.");
                return;
            }
            let stream = (event.streams && event.streams.length > 0)
                ? event.streams[0]
                : new MediaStream([event.track]);
            remoteAudio.srcObject = stream;
            remoteAudio.muted = false;
            remoteAudio.volume = 1.0;
            remoteAudio.play()
                .then(() => console.log("✅ Remote audio playing"))
                .catch(err => console.error("❌ Error playing remote audio:", err));
        };

        const offer = await this.pc.createOffer();
        await this.pc.setLocalDescription(offer);
        console.log("📞 Created offer:", offer.sdp);

        if (this.statsInterval) clearInterval(this.statsInterval);
        this.statsInterval = setInterval(() => this.pollAudioStats(), 5000);

        return offer.sdp;
    },

    // Answer an incoming call: returns the SDP answer string.
    answerCall: async function (sdpOffer) {
        if (this.pc) {
            this.pc.close();
        }

        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            console.log("🎙️ Local stream acquired for answering", this.localStream);
            console.log("Audio tracks (answer):", this.localStream.getAudioTracks());
        } catch (err) {
            console.error("❌ Error getting local stream:", err);
            return;
        }

        // (Optional) For debugging, attach the local stream.
        const localAudio = document.getElementById("localAudio");
        if (localAudio) {
            localAudio.srcObject = this.localStream;
            localAudio.play().catch(err => console.error("Error playing local audio (answer):", err));
        }

        const config = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };
        this.pc = new RTCPeerConnection(config);

        if (this.localStream && this.localStream.getAudioTracks().length > 0) {
            let audioTrack = this.localStream.getAudioTracks()[0];
            console.log("Audio track (answer) details:", {
                enabled: audioTrack.enabled,
                readyState: audioTrack.readyState,
                label: audioTrack.label
            });
            audioTrack.enabled = true;
            this.pc.addTrack(audioTrack, this.localStream);
            console.log("✅ Added local audio track via addTrack (answer).");
        } else {
            console.warn("⚠️ No local audio track available to add (answer).");
        }

        this.pc.onsignalingstatechange = () => {
            console.log("Signaling state (answer):", this.pc.signalingState);
        };
        this.pc.oniceconnectionstatechange = () => {
            console.log("ICE connection state (answer):", this.pc.iceConnectionState);
        };

        this.pc.onicecandidate = event => {
            if (event.candidate) {
                console.log("📡 Local ICE candidate (answer):", event.candidate.candidate);
                if (this.dotNetObject) {
                    this.dotNetObject.invokeMethodAsync('SendIceCandidate', JSON.stringify(event.candidate));
                } else {
                    console.log("❌ DotNetObjectReference not registered, queuing ICE candidate");
                    this.iceCandidatesQueue.push(event.candidate);
                }
            }
        };

        this.pc.ontrack = event => {
            console.log("🎧 Remote track received (answer):", event);
            const remoteAudio = document.getElementById("remoteAudio");
            if (!remoteAudio) {
                console.error("Remote audio element not found.");
                return;
            }
            let stream = (event.streams && event.streams.length > 0)
                ? event.streams[0]
                : new MediaStream([event.track]);
            remoteAudio.srcObject = stream;
            remoteAudio.muted = false;
            remoteAudio.volume = 1.0;
            remoteAudio.play()
                .then(() => console.log("✅ Remote audio playing (answer)"))
                .catch(err => console.error("❌ Error playing remote audio (answer):", err));
        };

        await this.pc.setRemoteDescription(new RTCSessionDescription({ type: "offer", sdp: sdpOffer }));
        const answer = await this.pc.createAnswer();
        await this.pc.setLocalDescription(answer);
        console.log("📞 Created answer:", answer.sdp);

        if (this.statsInterval) clearInterval(this.statsInterval);
        this.statsInterval = setInterval(() => this.pollAudioStats(), 5000);

        return answer.sdp;
    },

    // Add an ICE candidate received from the server.
    addIceCandidate: async function (candidateJson) {
        try {
            if (!this.pc) {
                console.warn("⚠️ No active RTCPeerConnection. Cannot add ICE candidate.");
                return;
            }
            let candidate;
            try {
                candidate = JSON.parse(candidateJson);
                if (typeof candidate === "object" && candidate.candidate) {
                    candidate = new RTCIceCandidate(candidate);
                    console.log("📡 Adding ICE candidate (from JSON):", candidate);
                } else {
                    throw new Error("Parsed candidate does not have expected properties.");
                }
            } catch (err) {
                console.warn("❌ Error parsing candidate JSON. Assuming candidateJson is an SDP string:", err);
                candidate = new RTCIceCandidate({ candidate: candidateJson });
                console.log("📡 Adding ICE candidate (from SDP string):", candidate);
            }
            await this.pc.addIceCandidate(candidate);
        } catch (err) {
            console.error("❌ Error adding ICE candidate:", err);
        }
    },

    // Close and clean up the connection.
    closePeer: function () {
        if (this.pc) {
            this.pc.onicecandidate = null;
            this.pc.ontrack = null;
            this.pc.close();
            console.log("🔴 RTCPeerConnection closed.");
            this.pc = null;
        }
        if (this.statsInterval) {
            clearInterval(this.statsInterval);
            this.statsInterval = null;
        }
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop());
            console.log("🔇 Local stream (microphone) stopped.");
            this.localStream = null;
        }
    },

    // Set remote description with an SDP answer.
    setRemoteDescription: async function (sdp) {
        if (!this.pc) {
            console.error("No active RTCPeerConnection to set remote description.");
            return;
        }
        try {
            await this.pc.setRemoteDescription(new RTCSessionDescription({ type: "answer", sdp: sdp }));
            console.log("✅ Remote description set with SDP answer.");
        } catch (err) {
            console.error("❌ Error setting remote description:", err);
        }
    }
};
