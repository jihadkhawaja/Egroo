/**
 * WebRTC Interop — ICE Server Configuration.
 * Handles ICE server normalization, relay detection, and transport policy.
 */
Object.assign(window.webrtcInterop, {
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
    }
});
