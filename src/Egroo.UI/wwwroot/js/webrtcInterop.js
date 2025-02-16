window.webrtcInterop = {
    localStream: null,
    peerConnection: null,

    startLocalStream: async function () {
        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            return true;
        } catch (err) {
            console.error("Error accessing microphone:", err);
            return false;
        }
    },

    stopLocalStream: function () {
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop()); // Stop all tracks (microphone)
            this.localStream = null;
        }
        if (this.peerConnection) {
            this.peerConnection.close();
            this.peerConnection = null;
        }
    },

    createPeerConnection: function (onSignalCallback, onIceCandidateCallback) {
        this.peerConnection = new RTCPeerConnection();

        // Add local stream to connection
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => {
                this.peerConnection.addTrack(track, this.localStream);
            });
        }

        // Handle ICE candidates
        this.peerConnection.onicecandidate = (event) => {
            if (event.candidate) {
                onIceCandidateCallback.invokeMethodAsync("Invoke", JSON.stringify(event.candidate));
            }
        };

        // Handle incoming stream
        this.peerConnection.ontrack = (event) => {
            console.log("Remote stream added.");
        };
    },

    async setRemoteDescription(description) {
        if (!this.peerConnection) {
            console.error("No active peer connection to set remote description.");
            return;
        }

        try {
            const desc = new RTCSessionDescription(JSON.parse(description));
            await this.peerConnection.setRemoteDescription(desc);
        } catch (error) {
            console.error("Error setting remote description:", error);
        }
    },

    async addIceCandidate(candidate) {
        if (!this.peerConnection) {
            console.error("No active peer connection to add ICE candidate.");
            return;
        }

        try {
            const iceCandidate = new RTCIceCandidate(JSON.parse(candidate));
            await this.peerConnection.addIceCandidate(iceCandidate);
        } catch (error) {
            console.error("Error adding ICE candidate:", error);
        }
    },

    closePeerConnection() {
        if (this.peerConnection) {
            this.peerConnection.close();
            this.peerConnection = null;
        }
    }
};
