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
