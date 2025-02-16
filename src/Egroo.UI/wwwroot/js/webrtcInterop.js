window.webrtcInterop = {
    localStream: null,
    peerConnection: null,
    onSignalCallback: null,
    onIceCandidateCallback: null,

    // Start capturing audio from the microphone.
    startLocalStream: async function () {
        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
            return true;
        } catch (err) {
            console.error("Error accessing microphone:", err);
            return false;
        }
    },

    // Stop capturing audio and close the peer connection.
    stopLocalStream: function () {
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop());
            this.localStream = null;
        }
        if (this.peerConnection) {
            this.peerConnection.close();
            this.peerConnection = null;
        }
    },

    // Create a peer connection.
    // The parameter 'isCaller' determines whether to create an offer immediately.
    createPeerConnection: function (onSignalCallback, onIceCandidateCallback, isCaller) {
        // Store the callbacks for later use.
        this.onSignalCallback = onSignalCallback;
        this.onIceCandidateCallback = onIceCandidateCallback;

        // Use a STUN server for ICE gathering.
        const configuration = {
            iceServers: [
                { urls: "stun:stun.l.google.com:19302" }
                // Add TURN servers here if needed.
            ]
        };

        this.peerConnection = new RTCPeerConnection(configuration);

        // Add each local track to the peer connection.
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => {
                this.peerConnection.addTrack(track, this.localStream);
            });
        }

        // When ICE candidates are gathered, send them via the callback.
        this.peerConnection.onicecandidate = (event) => {
            if (event.candidate && this.onIceCandidateCallback) {
                this.onIceCandidateCallback.invokeMethodAsync("Invoke", JSON.stringify(event.candidate));
            }
        };

        // When a remote track is received, attach it to an audio element.
        this.peerConnection.ontrack = (event) => {
            console.log("Remote stream added.");
            const remoteAudio = document.getElementById("remoteAudio");
            if (remoteAudio) {
                remoteAudio.srcObject = event.streams[0];
                remoteAudio.play().catch(err => console.error("Error playing remote audio:", err));
            }
        };

        // Only the caller automatically creates an offer.
        if (isCaller) {
            this.peerConnection.createOffer()
                .then(offer => this.peerConnection.setLocalDescription(offer))
                .then(() => {
                    if (this.onSignalCallback) {
                        this.onSignalCallback.invokeMethodAsync("Invoke", JSON.stringify(this.peerConnection.localDescription));
                    }
                })
                .catch(error => console.error("Error creating offer:", error));
        }
    },

    // Set the remote description.
    // If the description is an offer, automatically generate and send an answer.
    setRemoteDescription: async function (description) {
        if (!this.peerConnection) {
            console.error("No active peer connection to set remote description.");
            return;
        }

        try {
            const parsedDescription = JSON.parse(description);
            const desc = new RTCSessionDescription(parsedDescription);
            await this.peerConnection.setRemoteDescription(desc);

            // If we received an offer, create an answer.
            if (desc.type === "offer") {
                const answer = await this.peerConnection.createAnswer();
                await this.peerConnection.setLocalDescription(answer);
                if (this.onSignalCallback) {
                    this.onSignalCallback.invokeMethodAsync("Invoke", JSON.stringify(answer));
                }
            }
        } catch (error) {
            console.error("Error setting remote description:", error);
        }
    },

    // Add a remote ICE candidate.
    addIceCandidate: async function (candidate) {
        if (!this.peerConnection) {
            console.error("No active peer connection to add ICE candidate.");
            return;
        }

        try {
            const parsedCandidate = JSON.parse(candidate);
            const iceCandidate = new RTCIceCandidate(parsedCandidate);
            await this.peerConnection.addIceCandidate(iceCandidate);
        } catch (error) {
            console.error("Error adding ICE candidate:", error);
        }
    },

    // Manually close the peer connection.
    closePeerConnection: function () {
        if (this.peerConnection) {
            this.peerConnection.close();
            this.peerConnection = null;
        }
    }
};
