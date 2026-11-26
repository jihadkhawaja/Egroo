(function () {
    const events = [];
    const requests = new Map();
    const results = new Map();
    let database;

    const historyIndex = "historyByChannelAndDate";

    function putHistory(store, scope, message, key) {
        const date = Date.parse(message.dateSent ?? message.dateCreated);
        message._egrooHistoryScope = scope;
        message._egrooHistoryDate = Number.isFinite(date) ? date : 0;
        store.put(message, key);
    }

    function backfillHistoryIndex(store, range) {
        const batchSize = 512;
        const keys = store.getAllKeys(range, batchSize);
        const records = store.getAll(range, batchSize);
        records.onsuccess = () => {
            for (let index = 0; index < records.result.length; index++) {
                const message = records.result[index];
                const key = keys.result[index];
                const suffix = "/" + message?.referenceId;
                if (message?.referenceId && typeof key === "string" && key.endsWith(suffix))
                    putHistory(store, key.slice(0, -suffix.length), message, key);
            }

            if (keys.result.length === batchSize)
                backfillHistoryIndex(store, IDBKeyRange.lowerBound(keys.result.at(-1), true));
        };
    }

    function openCache() {
        database ??= new Promise((resolve, reject) => {
            const request = indexedDB.open("EgrooNative", 2);
            let blocked = false;
            request.onupgradeneeded = event => {
                const store = event.oldVersion === 0
                    ? request.result.createObjectStore("cache")
                    : request.transaction.objectStore("cache");
                store.createIndex(historyIndex, ["_egrooHistoryScope", "_egrooHistoryDate"]);
                if (event.oldVersion === 0) return;

                backfillHistoryIndex(store);
            };
            request.onblocked = () => {
                blocked = true;
                reject(new Error("Close any other Egroo tabs or windows and retry to update local history."));
            };
            request.onsuccess = () => {
                const db = request.result;
                if (blocked) {
                    db.close();
                    database = undefined;
                    return;
                }
                db.onversionchange = () => {
                    db.close();
                    database = undefined;
                };
                resolve(db);
            };
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
                if (current?.content != null) {
                    current.id = message.id;
                    current.content = message.content ?? current.content;
                    current.dateSeen ??= message.dateSeen;
                    current.dateSent = message.dateSent ?? current.dateSent;
                    putHistory(store, scope, current, id);
                } else if (!updateOnly && message.content != null) {
                    message.dateSeen = current?.dateSeen ?? message.dateSeen;
                    putHistory(store, scope, message, id);
                } else if (updateOnly && message.dateSeen != null && current?.dateSeen == null) {
                    // A receipt can arrive before the message. Keep it outside the history index.
                    store.put({ dateSeen: message.dateSeen }, id);
                }
            };
            tx.oncomplete = () => resolve();
            tx.onabort = () => reject(tx.error);
        });
    }

    async function readHistory(scope, limit) {
        if (limit !== null && limit <= 0) return [];
        const range = IDBKeyRange.bound([scope, Number.MIN_SAFE_INTEGER], [scope, Number.MAX_SAFE_INTEGER]);
        if (limit === null)
            return cache("readonly", store => store.index(historyIndex).getAll(range));

        const db = await openCache();
        return new Promise((resolve, reject) => {
            const tx = db.transaction("cache", "readonly");
            const request = tx.objectStore("cache").index(historyIndex).openCursor(range, "prev");
            const messages = [];
            request.onsuccess = () => {
                const cursor = request.result;
                if (!cursor) return;
                messages.push(cursor.value);
                if (messages.length < limit) cursor.continue();
            };
            tx.oncomplete = () => resolve(messages.reverse());
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
                                if (existing.result?.content == null && reference && record.content) {
                                    putHistory(store, scope, {
                                        ...record, referenceId: reference,
                                        dateSeen: existing.result?.dateSeen ?? record.dateSeen
                                    }, key);
                                }
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
        ["history.get", readHistory],
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
