# [Egroo](https://www.egroo.org/)

<img src="docs/icon.png" alt="Egroo icon" width="96" />

Egroo is a self-hosted chat platform built with .NET 10, Avalonia, ASP.NET Core, and SignalR. The API uses SQLite by default and also supports PostgreSQL. The Avalonia client runs on Windows, macOS, Linux, Android, iOS, and WebAssembly.

- Chat in channels with real-time messaging, typing indicators, and presence.
- Send end-to-end encrypted messages and attachments using device keys.
- Join channel voice calls over WebRTC.
- Create AI agents, chat with them directly, or mention them in channels.

## Run locally

### VS Code

With the .NET 10 SDK and the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) installed, open `Egroo.code-workspace`, select **Egroo (Desktop + API)** in **Run and Debug**, and press **F5**. VS Code starts the API at `http://localhost:5176`, then opens the Avalonia desktop app. Create an account from the sign-in screen. Stop the API debug session to end both sessions.

The local debug profile creates and migrates `src/Egroo.Server/App_Data/local-debug/egroo.db` at startup. The database and generated `local-debug-secrets.json` stay in that directory between runs. An optional `ConnectionStrings:Sqlite` setting can point to a different SQLite file, with relative paths resolved from the API content root.

### Terminal

Start the API and then the desktop client in separate terminals from the repository root. The `Local Debug` profiles are the defaults for both projects.

```sh
dotnet run --project src/Egroo.Server
dotnet run --project src/Egroo.Avalonia/Desktop
```

### PostgreSQL setup

Create a PostgreSQL database and a user that can apply migrations. Copy the [configuration template](src/Egroo.Server/appsettings.template.json) to `src/Egroo.Server/appsettings.Development.json` and set these values.

| Setting | Value |
|---|---|
| `Database:Provider` | `PostgreSQL` |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string for Egroo |
| `Secrets:Jwt` | Random JWT signing secret |
| `Encryption:Key` | Random 32-character ASCII key for server-side encryption |
| `Encryption:IV` | Random 16-character ASCII initialization vector |

Choose **Egroo (Desktop + PostgreSQL API)** in VS Code or start the API and desktop client in separate terminals. The PostgreSQL API listens on `http://localhost:5175`.

```sh
dotnet run --project src/Egroo.Server --launch-profile PostgreSQL
dotnet run --project src/Egroo.Avalonia/Desktop --launch-profile 'Egroo local'
```

See the [Avalonia build and hosting guide](docs/Avalonia-Client.md) for desktop prerequisites, mobile workloads, platform signing, and API address overrides.

## Build and test

```sh
dotnet build src/Egroo.slnx
dotnet test src/Egroo.Server.Test
```

The [main solution](src/Egroo.slnx) includes the desktop client, server, libraries, and tests. The [Avalonia solution](src/Egroo.Avalonia/Egroo.Avalonia.slnx) includes the browser and mobile projects for hosts with the corresponding workloads.

## Publish and self-host

Self-hosted APIs default to `App_Data/egroo.db` under their content root. Configure your own JWT and encryption secrets for a hosted deployment. Set `Database:Provider` to `PostgreSQL` and supply `ConnectionStrings:DefaultConnection` to use a PostgreSQL server. [Server configuration](wiki/Configuration.md)

Set `EgrooApiUrl` when publishing a client for your own server. To publish the WebAssembly client, install the workload and build against your public API address.

```sh
dotnet workload install wasm-tools
dotnet publish src/Egroo.Avalonia/Browser -c Release -p:EgrooApiUrl=https://api.example.com/
```

Serve the published `wwwroot` directory over HTTPS. Add the web app's origin to `Api:AllowedOrigins` on the API and forward WebSocket upgrades to `/chathub`. For voice across networks, configure ICE and TURN in the server's [voice settings](wiki/Configuration.md#voice-call-settings).

The repository also includes Dockerfiles for the [API](src/Egroo.Server/Dockerfile) and the [static WebAssembly client](src/Egroo.Avalonia/Browser/Dockerfile). The browser Dockerfile accepts `EGROO_API_URL` as a build argument.

## Project layout

| Project | Responsibility |
|---|---|
| [`Egroo.Avalonia`](src/Egroo.Avalonia) | Platform entry points for desktop, Android, iOS, and WebAssembly |
| [`Egroo.UI`](src/Egroo.UI) | Shared Avalonia controls and presentation services |
| [`Egroo.Client`](src/Egroo.Client) | HTTP and SignalR client services |
| [`Egroo.Contracts`](src/Egroo.Contracts) | Shared models and service contracts |
| [`Egroo.Server`](src/Egroo.Server) | API, authorization, persistence, and agent execution |
| [`Egroo.Server.SignalR`](src/Egroo.Server.SignalR) | Chat hub and connection tracking |
| [`Egroo.Server.Test`](src/Egroo.Server.Test) | Server and integration tests |

The server stores message metadata separately from pending encrypted payloads. It deletes each recipient's pending payload after acknowledgment. Device private keys and cached channel history live on the client. [Architecture](wiki/Architecture.md) · [API and SignalR reference](wiki/API-Documentation.md)

## Contributing

Read the [contributing guide](CONTRIBUTING.md) and [code of conduct](CODE_OF_CONDUCT.md). Report bugs or request features through [GitHub Issues](https://github.com/jihadkhawaja/Egroo/issues).

## License

Egroo is licensed under the [Apache License 2.0](LICENSE).
