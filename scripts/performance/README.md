# Browser performance benchmarks

Run from the repository root with Node.js 22 or later and Chrome installed. Set `EGROO_PERF_CHROMIUM` to a Chromium executable to use a different build.

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

## Active encrypted conversation

Publish a Release build of the server and browser application, then run the scenario against those outputs.

```sh
dotnet publish src/Egroo.Server -c Release -o artifacts/perf-server
dotnet publish src/Egroo.Avalonia/Browser -c Release -o artifacts/perf-browser
node scripts/performance/browser-conversation.mjs artifacts/perf-browser/wwwroot artifacts/perf-server
```

The script copies the server into a temporary directory, uses a fresh SQLite database, and binds the API and frontend to loopback. It creates disposable accounts and keeps all attachments in the test directory. It removes the directory and stops the API on exit.

The fixture uses a 1,280 × 900 Chromium viewport, seeds 150 encrypted records and opens that channel. A separate authenticated connection sends 32 messages, including four encrypted attachments and two opened image previews. The script then takes the receiving client offline, verifies two queued messages recover in order, opens the channel three more times, and signs out.

Compare baseline and candidate browser publishes on the same idle machine with the same server output. Save each JSON report with the Git commit and published configuration. The live frame metric starts at send time and looks for a change in the rendered region. Encryption and upload preparation happen before that timer. The report includes a separate server read receipt, persisted reconnect timing, long tasks, and Chromium heap snapshots before and after navigation. The heap snapshots do not measure the complete .NET or GPU footprint.

Canvas checks use fixed coordinates for the channel row, back button, and sign-out. They locate the preview action in the latest receiver bubble and verify that the decrypted fixture colors paint. Add `--inline-preview` when benchmarking a publish from before the preview flyout. Adjust those coordinates if the application layout changes. Run the scenario manually for comparison because compositor timing and release builds on a shared machine can vary.
## Voice call teardown

Use the same Release server and browser outputs with the microphone lifecycle scenario.

```sh
node scripts/performance/browser-media.mjs artifacts/perf-browser/wwwroot artifacts/perf-server
```

Two disposable browser sessions join a private conversation through the Avalonia UI. Chromium's synthetic microphone sends audio over a real loopback peer connection. The script verifies audio packets in both directions, six channel switches, sign-out, explicit leave, and rejoining. Separate checks exercise idle settings, denied and delayed capture, and interrupted or failed track replacement. The report records remaining tracks, peers, playback elements, and stats timers. The fixed canvas positions target the fixture viewport. Use the release acceptance runbook for microphone permissions, devices, and networks outside the fixture.
