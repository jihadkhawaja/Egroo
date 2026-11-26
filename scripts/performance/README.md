# Browser history benchmark

Run from the repository root with Node.js 20 or later and Chrome installed. Set `EGROO_PERF_CHROMIUM` to a Chromium executable to use a different build.

```sh
npm ci --prefix scripts/performance
node scripts/performance/browser-history.mjs
node scripts/performance/browser-history.mjs --cursor
```

Pass the path to a previous copy of `src/Egroo.UI/wwwroot/js/clientRuntime.js` to measure that copy against the current runtime on the same machine. Use `EGROO_PERF_RUNTIME` to point the candidate at a published copy of the runtime instead. The two runs use fresh browser contexts and identical data.

```sh
node scripts/performance/browser-history.mjs /path/to/baseline/clientRuntime.js
```

The fixture loads 15,000 version 1 IndexedDB records with 512-character payloads and reads the latest 150. It reports the first read including migration, nine warm samples, and the warm median. Assertions cover the exact chronological page, a timestamp tie at the page boundary, an empty channel, a zero limit, channel isolation, and preservation of the existing migration marker. The second command exercises the cursor used on browsers without directional batch reads.

Run the comparison on an idle machine and record the JSON output with each result. The test covers the browser storage path, from the runtime request to deserialized response. Startup, timeline rendering, transport, media, and attachments require separate measurements.
