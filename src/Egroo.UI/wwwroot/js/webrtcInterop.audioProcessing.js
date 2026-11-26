/**
 * WebRTC Interop — Audio Processing.
 * Handles microphone constraints, noise suppression, echo cancellation,
 * auto gain control, track recreation, and preference persistence.
 */
Object.assign(window.webrtcInterop, {
    acquireLocalStream: async function (generation) {
        try {
            this._loadPersistedAudioProcessingPreferences();
            const stream = await navigator.mediaDevices.getUserMedia(this._buildUserMediaConstraints());
            if (generation !== this.callGeneration || !stream.getAudioTracks().some(track => track.readyState === "live")) {
                stream.getTracks().forEach(track => track.stop());
                return false;
            }

            this.localStream = stream;
            console.log("[WebRTC] Local audio stream acquired:", stream.getAudioTracks().length, "audio tracks");
            return true;
        } catch (err) {
            console.error("[WebRTC] Error acquiring microphone:", err);
            return false;
        }
    },

    toggleMute: function () {
        if (!this.localStream) return this.isMuted;
        this.isMuted = !this.isMuted;
        for (const stream of [this.localStream, ...this.pendingAudioStreams])
            stream.getAudioTracks().forEach(track => { track.enabled = !this.isMuted; });
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

        if (this.audioSettingsUpdate)
            return this._createAudioProcessingState(false);

        const previousEnabled = this[enabledProperty];
        this[enabledProperty] = !!enabled;
        this._persistAudioProcessingPreferences();

        if (!this[supportedProperty])
            return this._createAudioProcessingState(false);

        // Store preferences without opening the microphone. The next join uses these constraints.
        if (!this.localStream)
            return this._createAudioProcessingState(true);

        const generation = this.callGeneration;
        const update = this.audioSettingsUpdate = {};
        try {
            const track = await this._recreateLocalAudioTrack(generation);
            const wasApplied = this._verifyAudioProcessingSetting(track, settingName, this[enabledProperty]);
            return this._createAudioProcessingState(wasApplied);
        } catch (err) {
            if (generation === this.callGeneration) {
                this[enabledProperty] = previousEnabled;
                this._persistAudioProcessingPreferences();
            }
            console.error("[WebRTC] Error applying audio processing setting:", settingName, err);
            return this._createAudioProcessingState(false);
        } finally {
            if (this.audioSettingsUpdate === update)
                this.audioSettingsUpdate = null;
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

    _recreateLocalAudioTrack: async function (generation) {
        const previousStream = this.localStream;
        const newStream = await navigator.mediaDevices.getUserMedia(this._buildUserMediaConstraints());
        this.pendingAudioStreams.add(newStream);

        try {
            if (generation !== this.callGeneration)
                return null;

            const newTrack = newStream.getAudioTracks()[0];
            if (!newTrack)
                throw new Error("No audio track was returned when recreating the microphone stream.");

            const replaced = new Map();
            newTrack.enabled = !this.isMuted;
            while (true) {
                const replacements = [];
                for (const peer of this.peers.values()) {
                    for (const sender of peer.pc.getSenders()) {
                        if (sender.track?.kind === "audio" && !replaced.has(sender)) {
                            replaced.set(sender, sender.track);
                            replacements.push({ peer, sender });
                        }
                    }
                }
                if (replacements.length === 0)
                    break;

                const outcomes = await Promise.allSettled(
                    replacements.map(async ({ sender }) => sender.replaceTrack(newTrack)));
                if (generation !== this.callGeneration)
                    return null;

                const currentPeers = new Set(this.peers.values());
                const failed = outcomes.find((outcome, index) =>
                    outcome.status === "rejected" && currentPeers.has(replacements[index].peer));
                if (failed) {
                    await Promise.allSettled(Array.from(replaced, async ([sender, previousTrack]) => {
                        if (sender.track === newTrack)
                            await sender.replaceTrack(previousTrack);
                    }));
                    throw failed.reason;
                }
                // Check senders attached by negotiation while the previous updates were pending.
            }

            newTrack.enabled = !this.isMuted;
            this.localStream = newStream;
            previousStream.getTracks().forEach(track => track.stop());
            return newTrack;
        } finally {
            this.pendingAudioStreams.delete(newStream);
            if (this.localStream !== newStream)
                newStream.getTracks().forEach(track => track.stop());
        }
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
