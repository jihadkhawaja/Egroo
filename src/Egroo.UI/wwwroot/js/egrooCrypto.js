(function () {
    const textEncoder = new TextEncoder();
    const textDecoder = new TextDecoder();

    function bytesToBase64(bytes) {
        let binary = "";
        const chunkSize = 0x8000;

        for (let index = 0; index < bytes.length; index += chunkSize) {
            const chunk = bytes.subarray(index, index + chunkSize);
            binary += String.fromCharCode(...chunk);
        }

        return btoa(binary);
    }

    function base64ToBytes(base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let index = 0; index < binary.length; index++) {
            bytes[index] = binary.charCodeAt(index);
        }

        return bytes;
    }

    async function importPublicKey(publicKeyBase64) {
        return crypto.subtle.importKey(
            "spki",
            base64ToBytes(publicKeyBase64),
            { name: "RSA-OAEP", hash: "SHA-256" },
            true,
            ["encrypt"]);
    }

    async function importPrivateKey(privateKeyBase64) {
        return crypto.subtle.importKey(
            "pkcs8",
            base64ToBytes(privateKeyBase64),
            { name: "RSA-OAEP", hash: "SHA-256" },
            true,
            ["decrypt"]);
    }

    async function sha256Hex(bytes) {
        const digest = await crypto.subtle.digest("SHA-256", bytes);
        return Array.from(new Uint8Array(digest))
            .map(value => value.toString(16).padStart(2, "0"))
            .join("");
    }

    window.egrooCrypto = {
        async generateIdentity() {
            const keyPair = await crypto.subtle.generateKey(
                {
                    name: "RSA-OAEP",
                    modulusLength: 2048,
                    publicExponent: new Uint8Array([1, 0, 1]),
                    hash: "SHA-256"
                },
                true,
                ["encrypt", "decrypt"]);

            const publicKeyBytes = new Uint8Array(await crypto.subtle.exportKey("spki", keyPair.publicKey));
            const privateKeyBytes = new Uint8Array(await crypto.subtle.exportKey("pkcs8", keyPair.privateKey));
            const keyId = (await sha256Hex(publicKeyBytes)).slice(0, 32);

            return {
                publicKey: bytesToBase64(publicKeyBytes),
                privateKey: bytesToBase64(privateKeyBytes),
                keyId
            };
        },

        async encryptMessageForRecipients(request) {
            const aesKey = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
            const iv = crypto.getRandomValues(new Uint8Array(12));
            const cipherBuffer = await crypto.subtle.encrypt(
                { name: "AES-GCM", iv },
                aesKey,
                textEncoder.encode(request.plaintext));
            const rawAesKey = new Uint8Array(await crypto.subtle.exportKey("raw", aesKey));
            const cipherBytes = new Uint8Array(cipherBuffer);

            const results = [];
            for (const recipient of request.recipients) {
                // Multi-device: recipient may have multiple keys
                if (recipient.keys && recipient.keys.length > 0) {
                    const wrappedKeys = [];
                    for (const keyEntry of recipient.keys) {
                        const publicKey = await importPublicKey(keyEntry.publicKey);
                        const wrappedKey = new Uint8Array(await crypto.subtle.encrypt({ name: "RSA-OAEP" }, publicKey, rawAesKey));
                        wrappedKeys.push({
                            kid: keyEntry.keyId || null,
                            wk: bytesToBase64(wrappedKey)
                        });
                    }

                    results.push({
                        userId: recipient.userId || null,
                        agentDefinitionId: recipient.agentDefinitionId || null,
                        content: JSON.stringify({
                            v: 2,
                            alg: "RSA-OAEP-256/A256GCM",
                            iv: bytesToBase64(iv),
                            ct: bytesToBase64(cipherBytes),
                            keys: wrappedKeys
                        })
                    });
                } else {
                    // Single key (legacy / agents)
                    const publicKey = await importPublicKey(recipient.publicKey);
                    const wrappedKey = new Uint8Array(await crypto.subtle.encrypt({ name: "RSA-OAEP" }, publicKey, rawAesKey));

                    results.push({
                        userId: recipient.userId || null,
                        agentDefinitionId: recipient.agentDefinitionId || null,
                        content: JSON.stringify({
                            v: 1,
                            alg: "RSA-OAEP-256/A256GCM",
                            kid: recipient.keyId || null,
                            iv: bytesToBase64(iv),
                            ct: bytesToBase64(cipherBytes),
                            wk: bytesToBase64(wrappedKey)
                        })
                    });
                }
            }

            return results;
        },

        async decryptMessage(request) {
            if (!request || !request.payload) {
                return { status: "empty", plaintext: "" };
            }

            let payload;
            try {
                payload = JSON.parse(request.payload);
            }
            catch {
                return { status: "plain", plaintext: request.payload };
            }

            if (!payload || !payload.iv || !payload.ct) {
                return { status: "plain", plaintext: request.payload };
            }

            if (!request.privateKey) {
                return { status: "missing-key", plaintext: null };
            }

            // v2 envelope: multiple wrapped keys
            if (payload.v === 2 && payload.keys && payload.keys.length > 0) {
                const privateKey = await importPrivateKey(request.privateKey);

                for (const keyEntry of payload.keys) {
                    try {
                        const rawAesKey = await crypto.subtle.decrypt(
                            { name: "RSA-OAEP" },
                            privateKey,
                            base64ToBytes(keyEntry.wk));
                        const aesKey = await crypto.subtle.importKey("raw", rawAesKey, { name: "AES-GCM" }, false, ["decrypt"]);
                        const plainBuffer = await crypto.subtle.decrypt(
                            { name: "AES-GCM", iv: base64ToBytes(payload.iv) },
                            aesKey,
                            base64ToBytes(payload.ct));

                        return { status: "decrypted", plaintext: textDecoder.decode(plainBuffer) };
                    }
                    catch {
                        // This wrapped key doesn't match our private key, try next
                    }
                }

                return { status: "failed", plaintext: null };
            }

            // v1 envelope: single wrapped key
            if (payload.v !== 1 || !payload.wk) {
                return { status: "plain", plaintext: request.payload };
            }

            try {
                const privateKey = await importPrivateKey(request.privateKey);
                const rawAesKey = await crypto.subtle.decrypt(
                    { name: "RSA-OAEP" },
                    privateKey,
                    base64ToBytes(payload.wk));
                const aesKey = await crypto.subtle.importKey("raw", rawAesKey, { name: "AES-GCM" }, false, ["decrypt"]);
                const plainBuffer = await crypto.subtle.decrypt(
                    { name: "AES-GCM", iv: base64ToBytes(payload.iv) },
                    aesKey,
                    base64ToBytes(payload.ct));

                return { status: "decrypted", plaintext: textDecoder.decode(plainBuffer) };
            }
            catch {
                return { status: "failed", plaintext: null };
            }
        },

        async encryptFile(request) {
            const aesKey = await crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
            const iv = crypto.getRandomValues(new Uint8Array(12));
            const plainBytes = base64ToBytes(request.fileBase64);
            const cipherBuffer = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, aesKey, plainBytes);
            const rawAesKey = new Uint8Array(await crypto.subtle.exportKey("raw", aesKey));

            return {
                encryptedBase64: bytesToBase64(new Uint8Array(cipherBuffer)),
                keyBase64: bytesToBase64(rawAesKey),
                ivBase64: bytesToBase64(iv)
            };
        },

        async decryptFile({ encryptedBase64, keyBase64, ivBase64 }) {
            const key = await crypto.subtle.importKey("raw", base64ToBytes(keyBase64), "AES-GCM", false, ["decrypt"]);
            const bytes = await crypto.subtle.decrypt(
                { name: "AES-GCM", iv: base64ToBytes(ivBase64) }, key, base64ToBytes(encryptedBase64));
            return bytesToBase64(new Uint8Array(bytes));
        }
    };

})();
