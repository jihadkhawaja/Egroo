window.webrtcInterop = {
    pc: null,
    localStream: null,
    iceCandidatesQueue: [], // To store ICE candidates until DotNetObjectReference is registered
    statsInterval: null, // Interval ID for polling stats

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

    // Start a call as the caller
    startCall: async function () {
        // Clean up any existing connection
        if (this.pc) {
            this.pc.close();
        }

        // Request audio with additional constraints
        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
                video: false
            });
            console.log("🎙️ Local stream acquired", this.localStream);
            if (this.localStream.getAudioTracks().length > 0) {
                const audioTrack = this.localStream.getAudioTracks()[0];
                audioTrack.enabled = true;
                console.log("Local audio track properties:", {
                    enabled: audioTrack.enabled,
                    readyState: audioTrack.readyState,
                    label: audioTrack.label
                });
            }
        } catch (err) {
            console.error("❌ Error getting local stream:", err);
            return;
        }

        // Create RTCPeerConnection
        const config = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };
        this.pc = new RTCPeerConnection(config);

        // Add the local audio track via addTrack
        if (this.localStream && this.localStream.getAudioTracks().length > 0) {
            let audioTrack = this.localStream.getAudioTracks()[0];
            this.pc.addTrack(audioTrack, this.localStream);
            console.log("✅ Added local audio track via addTrack.");
        } else {
            console.warn("⚠️ No local audio track available to add.");
        }

        // Handle ICE candidate event
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

        // Handle remote track event
        this.pc.ontrack = event => {
            console.log("🎧 Remote track received:", event);
            const remoteAudio = document.getElementById("remoteAudio");
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

        // Create the SDP offer
        const offer = await this.pc.createOffer();
        await this.pc.setLocalDescription(offer);
        console.log("📞 Created offer:", offer.sdp);

        // Start polling audio stats every 5 seconds
        if (this.statsInterval) clearInterval(this.statsInterval);
        this.statsInterval = setInterval(() => this.pollAudioStats(), 5000);

        return offer.sdp;
    },

    // Answer an incoming call
    answerCall: async function (sdpOffer) {
        if (this.pc) {
            this.pc.close();
        }

        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({
                audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
                video: false
            });
            console.log("🎙️ Local stream acquired for answering", this.localStream);
            if (this.localStream.getAudioTracks().length > 0) {
                const audioTrack = this.localStream.getAudioTracks()[0];
                audioTrack.enabled = true;
                console.log("Local audio track properties (answer):", {
                    enabled: audioTrack.enabled,
                    readyState: audioTrack.readyState,
                    label: audioTrack.label
                });
            }
        } catch (err) {
            console.error("❌ Error getting local stream:", err);
            return;
        }

        const config = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };
        this.pc = new RTCPeerConnection(config);

        // Add the local audio track via addTrack
        if (this.localStream && this.localStream.getAudioTracks().length > 0) {
            let audioTrack = this.localStream.getAudioTracks()[0];
            this.pc.addTrack(audioTrack, this.localStream);
            console.log("✅ Added local audio track via addTrack (answer).");
        } else {
            console.warn("⚠️ No local audio track available to add (answer).");
        }

        // Handle ICE candidate event
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

        // Handle remote track event
        this.pc.ontrack = event => {
            console.log("🎧 Remote track received:", event);
            const remoteAudio = document.getElementById("remoteAudio");
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

        await this.pc.setRemoteDescription(new RTCSessionDescription({ type: "offer", sdp: sdpOffer }));

        const answer = await this.pc.createAnswer();
        await this.pc.setLocalDescription(answer);
        console.log("📞 Created answer:", answer.sdp);

        if (this.statsInterval) clearInterval(this.statsInterval);
        this.statsInterval = setInterval(() => this.pollAudioStats(), 5000);

        return answer.sdp;
    },

    addIceCandidate: async function (candidateJson) {
        try {
            if (!this.pc) {
                console.warn("⚠️ No active RTCPeerConnection. Cannot add ICE candidate.");
                return;
            }
            let candidate;
            try {
                const parsedCandidate = JSON.parse(candidateJson);
                if (parsedCandidate && parsedCandidate.candidate) {
                    candidate = new RTCIceCandidate({
                        candidate: parsedCandidate.candidate,
                        sdpMid: parsedCandidate.sdpMid,
                        sdpMLineIndex: parsedCandidate.sdpMLineIndex,
                        usernameFragment: parsedCandidate.usernameFragment
                    });
                    console.log("📡 Adding ICE candidate:", candidate);
                } else {
                    throw new Error("Invalid candidate format");
                }
            } catch (err) {
                console.error("❌ Error parsing ICE candidate JSON or invalid format:", err);
                return;
            }
            await this.pc.addIceCandidate(candidate);
        } catch (err) {
            console.error("❌ Error adding ICE candidate:", err);
        }
    },

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
