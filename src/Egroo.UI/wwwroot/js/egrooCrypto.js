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

    function base64UrlToText(base64Url) {
        let padded = base64Url.replace(/-/g, "+").replace(/_/g, "/");
        while (padded.length % 4 !== 0) {
            padded += "=";
        }

        return textDecoder.decode(base64ToBytes(padded));
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

    async function downloadEncryptedFile(tokenBase64Url) {
        const metadata = JSON.parse(base64UrlToText(tokenBase64Url));
        const jwt = localStorage.getItem("token");

        if (!jwt) {
            throw new Error("Missing access token.");
        }

        const response = await fetch(metadata.Url, {
            headers: {
                Authorization: `Bearer ${jwt}`
            }
        });

        if (!response.ok) {
            throw new Error(`Failed to download encrypted file: ${response.status}`);
        }

        const cipherBytes = new Uint8Array(await response.arrayBuffer());
        const aesKey = await crypto.subtle.importKey(
            "raw",
            base64ToBytes(metadata.Key),
            { name: "AES-GCM" },
            false,
            ["decrypt"]);

        const plainBuffer = await crypto.subtle.decrypt(
            { name: "AES-GCM", iv: base64ToBytes(metadata.Iv) },
            aesKey,
            cipherBytes);

        const blob = new Blob([plainBuffer], { type: metadata.ContentType || "application/octet-stream" });
        const objectUrl = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = objectUrl;
        anchor.download = metadata.FileName || "download";
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(objectUrl);
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

            if (!payload || payload.v !== 1 || !payload.wk || !payload.iv || !payload.ct) {
                return { status: "plain", plaintext: request.payload };
            }

            if (!request.privateKey) {
                return { status: "missing-key", plaintext: null };
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

        downloadEncryptedFile
    };

    document.addEventListener("click", function (event) {
        const target = event.target instanceof Element
            ? event.target.closest(".egroo-encrypted-file")
            : null;

        if (!target) {
            return;
        }

        event.preventDefault();
        const token = target.getAttribute("data-egroo-file");
        if (!token) {
            return;
        }

        downloadEncryptedFile(token).catch(console.error);
    });
})();