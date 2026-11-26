# Avalonia client

Egroo uses the shared Avalonia controls in `src/Egroo.UI` on Windows, macOS, Linux, Android, iOS, and WebAssembly. The client connects directly to `Egroo.Server` for HTTP and SignalR. Native targets use the API's small `/runtime/` page for WebRTC, Web Crypto, and local persistence. The WebAssembly build includes the runtime scripts and serves the chat interface from static files.

The main solution is `src/Egroo.slnx`. The Avalonia solution at `src/Egroo.Avalonia/Egroo.Avalonia.slnx` includes the desktop, mobile, and browser targets.

## Desktop

Run the API and desktop client from separate terminals with the .NET 10 SDK:

```sh
dotnet run --project src/Egroo.Server
dotnet run --project src/Egroo.Avalonia/Desktop
```

The default desktop launch profile targets `http://localhost:5176/` for the local SQLite API. Packaged apps use `https://api.egroo.org/` by default. Set `EGROO_API_URL` before starting the executable, or pass `-p:EgrooApiUrl=https://api.example.com/` when publishing.

```sh
dotnet publish src/Egroo.Avalonia/Desktop -c Release -r win-x64 --self-contained
dotnet publish src/Egroo.Avalonia/Desktop -c Release -r osx-arm64 --self-contained
dotnet publish src/Egroo.Avalonia/Desktop -c Release -r linux-x64 --self-contained
```

The macOS publish also creates `Egroo.app`. Windows uses the Edge WebView2 Runtime. Linux uses WPE WebKit or WebKitGTK for the media runtime. [Avalonia WebView prerequisites](https://docs.avaloniaui.net/docs/app-development/embedding-web-content)

## Android and iOS

Use an HTTPS Egroo API address reachable from the device. Add `-p:EgrooApiUrl=https://api.example.com/` to either build for a self-hosted API. Android requires Android SDK Platform 36 and a .NET-compatible JDK. iOS device and distribution builds use Xcode and Apple signing on macOS.

```sh
dotnet workload install android
dotnet build src/Egroo.Avalonia/Android -t:Run

dotnet workload install ios
dotnet build src/Egroo.Avalonia/iOS -t:Run -p:RuntimeIdentifier=iossimulator-arm64
```

## WebAssembly

```sh
dotnet workload install wasm-tools
dotnet publish src/Egroo.Avalonia/Browser -c Release -p:EgrooApiUrl=https://api.example.com/
```

Serve the published `wwwroot` directory over HTTPS. The API must allow the browser origin in `Api:AllowedOrigins`. The browser Dockerfile builds and serves these static assets on port 8080. Set its `EGROO_API_URL` build argument for a self-hosted API.

On an existing production browser session, the new client can import the previous browser's device key and local channel history when served from the same origin. Native app history and private keys in another WebView origin remain in that origin's storage.

## Encrypted channel delivery

A message sent by a person is delivered to every active session for each addressed channel member. A device can decrypt its copy when the message includes that device's enrolled key.

On reconnect, the requesting session receives that user's pending messages in sent order. A message remains pending for the user until one device saves and acknowledges it. Each device keeps its own local message history, and newly enrolled devices start with their own history.

## Container images

The **Avalonia containers** workflow builds and smoke-tests both images on pull requests. After the workflow reaches the default branch, a maintainer can run it there to publish a paired release to GitHub Container Registry. The workflow summary lists the Linux x64 image references.

Both image names share a release tag derived from the source revision, workflow run, and attempt. Use the same tag for the API and browser. Use image digests if your deployment needs immutable references. Public images can be pulled anonymously. For a private package, sign in to `ghcr.io` with a GitHub personal access token (classic) that has `read:packages` and access to the package.

Create `egroo-api.env` with the following settings. Supply your own values and protect the file as you would your server configuration.

```dotenv
Secrets__Jwt=<random secret of at least 32 characters>
Encryption__Key=<random 32-character ASCII key>
Encryption__IV=<random 16-character ASCII IV>
Api__AllowedOrigins__0=https://chat.example.com
```

Create `egroo-config.json` beside it with the URL that browsers use to reach the API. This file is served publicly, so keep credentials in the API configuration.

```json
{
  "apiUrl": "https://api.example.com/"
}
```

Use the tag from the workflow and start the pair. These commands bind locally for a reverse proxy and use SQLite in a persistent Docker volume.

```sh
tag='<paired release tag>'
docker run -d --name egroo-api --restart unless-stopped \
  -p 127.0.0.1:8081:8080 --env-file ./egroo-api.env \
  --mount type=volume,source=egroo-data,target=/app/App_Data \
  "ghcr.io/jihadkhawaja/egroo-api:$tag"
docker run -d --name egroo-browser --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  --mount "type=bind,source=$PWD/egroo-config.json,target=/usr/share/nginx/html/egroo-config.json,readonly" \
  "ghcr.io/jihadkhawaja/egroo-browser:$tag"
```

Serve the browser and API over HTTPS and forward WebSocket upgrades to the API's `/chathub` endpoint. A static deployment can set the same `egroo-config.json` beside `index.html`. An empty JSON object (`{}`) uses the API URL selected at build time.

Record the installed pair and back up the API database before upgrading. To roll back, recreate both containers with the previous pair and preserve the API settings and persistent volume. If the newer API applied incompatible migrations, restore the corresponding database backup before restarting it.
