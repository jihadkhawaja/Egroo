(function () {
    const events = [];
    const requests = new Map();
    const results = new Map();
    let database;

    function openCache() {
        database ??= new Promise((resolve, reject) => {
            const request = indexedDB.open("EgrooNative", 1);
            request.onupgradeneeded = () => request.result.createObjectStore("cache");
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => { database = undefined; reject(request.error); };
        });
        return database;
    }

    async function cache(mode, operation) {
        const db = await openCache();
        return new Promise((resolve, reject) => {
            const tx = db.transaction("cache", mode);
            const request = operation(tx.objectStore("cache"));
            tx.oncomplete = () => resolve(request.result);
            tx.onabort = () => reject(tx.error);
        });
    }

    async function storeMessage(scope, message, updateOnly) {
        const db = await openCache();
        return new Promise((resolve, reject) => {
            const tx = db.transaction("cache", "readwrite");
            const store = tx.objectStore("cache");
            const id = scope + "/" + message.referenceId;
            const request = store.get(id);
            request.onsuccess = () => {
                const current = request.result;
                if (current) {
                    current.id = message.id;
                    current.content = message.content ?? current.content;
                    current.dateSeen = message.dateSeen ?? current.dateSeen;
                    current.dateSent = message.dateSent ?? current.dateSent;
                    store.put(current, id);
                } else if (!updateOnly && message.content !== null) {
                    store.put(message, id);
                }
            };
            tx.oncomplete = () => resolve();
            tx.onabort = () => reject(tx.error);
        });
    }

    async function importLegacyHistory(scope, channel) {
        const marker = "legacy-import:" + scope;
        if (await cache("readonly", store => store.get(marker))) return;
        const legacy = await new Promise((resolve, reject) => {
            const request = indexedDB.open("EgrooDatabase");
            let missing = false;
            request.onupgradeneeded = () => { missing = true; request.transaction.abort(); };
            request.onerror = () => missing ? resolve(null) : reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
        if (legacy) {
            try {
                const name = [...legacy.objectStoreNames].find(value => value.toLowerCase() === "messages");
                if (name) {
                    const records = await new Promise((resolve, reject) => {
                        const store = legacy.transaction(name, "readonly").objectStore(name);
                        const request = store.getAll();
                        request.onsuccess = () => resolve(request.result);
                        request.onerror = () => reject(request.error);
                    });
                    const db = await openCache();
                    await new Promise((resolve, reject) => {
                        const tx = db.transaction("cache", "readwrite");
                        const store = tx.objectStore("cache");
                        for (const record of records.filter(item => item.channelId === channel)) {
                            const reference = record.referenceId ?? record.id;
                            const key = scope + "/" + reference;
                            const existing = store.get(key);
                            existing.onsuccess = () => {
                                if (!existing.result && reference && record.content)
                                    store.put({ ...record, referenceId: reference }, key);
                            };
                        }
                        tx.oncomplete = resolve;
                        tx.onabort = () => reject(tx.error);
                    });
                }
            } finally {
                legacy.close();
            }
        }
        await cache("readwrite", store => store.put(true, marker));
    }

    webrtcInterop.registerDotNetObject({
        invokeMethodAsync(name, ...args) {
            events.push([name, String(args[0] ?? ""), String(args[1] ?? "")]);
            return Promise.resolve();
        }
    });

    const methods = new Map([
        ["storage.get", key => localStorage.getItem(key)],
        ["storage.set", (key, value) => localStorage.setItem(key, value)],
        ["storage.remove", key => localStorage.removeItem(key)],
        ["history.get", async (scope, limit) => {
            const range = IDBKeyRange.bound(scope + "/", scope + "/\uffff");
            const messages = await cache("readonly", store => store.getAll(range));
            messages.sort((a, b) => Date.parse(a.dateSent ?? a.dateCreated) - Date.parse(b.dateSent ?? b.dateCreated));
            return limit === null ? messages : messages.slice(-limit);
        }],
        ["history.put", storeMessage],
        ["history.importLegacy", importLegacyHistory],
        ["media.events", () => events.splice(0)],
        ["egrooCrypto.generateIdentity", egrooCrypto.generateIdentity],
        ["egrooCrypto.encryptMessageForRecipients", egrooCrypto.encryptMessageForRecipients],
        ["egrooCrypto.decryptMessage", egrooCrypto.decryptMessage],
        ["egrooCrypto.encryptFile", egrooCrypto.encryptFile],
        ["egrooCrypto.decryptFile", egrooCrypto.decryptFile],
        ["file.save", (base64, name) => {
            const url = URL.createObjectURL(new Blob([Uint8Array.from(atob(base64), char => char.charCodeAt(0))]));
            const link = document.createElement("a");
            link.href = url;
            link.download = name;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        }]
    ]);

    for (const name of [
        "configureIceServers", "joinCall", "leaveCall", "toggleMute", "setAudioProcessingSetting",
        "getAudioProcessingState", "removePeer", "createOfferForPeer", "handleOfferFromPeer",
        "handleAnswerFromPeer", "addIceCandidateForPeer", "ensurePeerForParticipant"
    ]) {
        if (typeof webrtcInterop[name] === "function")
            methods.set("webrtcInterop." + name, webrtcInterop[name].bind(webrtcInterop));
    }

    async function invoke(request) {
        try {
            const { method, args } = JSON.parse(request);
            const handler = methods.get(method);
            if (!handler) throw new Error("Unsupported runtime operation.");
            return JSON.stringify({ value: (await handler(...args)) ?? null });
        } catch (error) {
            return JSON.stringify({ error: error.message || String(error) });
        }
    }

    globalThis.egrooRuntime = {
        invoke,
        append(id, chunk) {
            requests.set(id, (requests.get(id) ?? "") + chunk);
        },
        async start(id) {
            results.set(id, null);
            try {
                const bytes = Uint8Array.from(atob(requests.get(id)), char => char.charCodeAt(0));
                const result = await invoke(new TextDecoder().decode(bytes));
                if (results.has(id)) results.set(id, result);
            } finally {
                requests.delete(id);
            }
        },
        read(id, offset) {
            return results.get(id)?.slice(offset, offset + 16384) ?? null;
        },
        clear(id) {
            requests.delete(id);
            results.delete(id);
        }
    };
})();
