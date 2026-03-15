/**
 * WebRTC Interop — Audio Processing.
 * Handles microphone constraints, noise suppression, echo cancellation,
 * auto gain control, track recreation, and preference persistence.
 */
Object.assign(window.webrtcInterop, {
    acquireLocalStream: async function () {
        if (this.localStream) return true;
        try {
            this._loadPersistedAudioProcessingPreferences();
            this.localStream = await navigator.mediaDevices.getUserMedia(this._buildUserMediaConstraints());
            console.log("[WebRTC] Local audio stream acquired:", this.localStream.getAudioTracks().length, "audio tracks");
            return true;
        } catch (err) {
            console.error("[WebRTC] Error acquiring microphone:", err);
            return false;
        }
    },

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
            return this._createAudioProcessingState(wasApplied);
        } catch (err) {
            this[enabledProperty] = previousEnabled;
            this._persistAudioProcessingPreferences();
            console.error("[WebRTC] Error applying audio processing setting:", settingName, err);
            return this._createAudioProcessingState(false);
        }
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
    }
});
