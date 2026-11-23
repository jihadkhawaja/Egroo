# Getting Started

This page is the fastest reliable path from a fresh clone to a working local Egroo environment.

## What You Are Starting

For local development, Egroo runs as:

- a local SQLite database
- the API and SignalR backend from `src/Egroo.Server`
- the Avalonia desktop client from `src/Egroo.Avalonia/Desktop`

Default development URLs:

- API: `http://localhost:5176`
- Swagger: `http://localhost:5176/swagger`
- Desktop app: the Avalonia window opens when the client starts

## Prerequisites

Install these first:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/downloads)

Optional but useful:

- Visual Studio 2022 or Visual Studio Code
- PostgreSQL and a database client when using that provider
- Docker Desktop if you want container-based database or deployment workflows

## 1. Clone The Repository

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

## 2. Start The API

In VS Code, open `Egroo.code-workspace` and press **F5** with **Egroo (Desktop + API)** selected to start both projects. To start the API from a terminal, run this command.

```bash
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
```

What to expect:

- the API listens on `http://localhost:5176`
- Swagger is available in development
- the API creates and migrates `src/Egroo.Server/App_Data/local-debug/egroo.db` and stores its generated credentials in the same directory

## 3. Start The Desktop App

Open a second terminal:

```bash
dotnet run --project src/Egroo.Avalonia/Desktop
```

Open the Avalonia desktop window.

The default desktop profile connects to `http://localhost:5176/`. See [Configuration](Configuration) to use PostgreSQL instead.

## 4. Create An Account And Publish Your Device Key

Create an account and sign in.

Current chat delivery can use per-recipient end-to-end encrypted payloads. On first sign-in, the client can generate a device key pair locally and publish the public key to the server. The private key stays on the device.

What happens next:

1. Your browser creates a local key pair.
2. The public key and `keyId` are published to the server for this device.
3. Senders encrypt message content for your published key.
4. Your device decrypts received ciphertext locally.
5. After delivery succeeds, the client acknowledges the pending message so the server can remove the stored ciphertext for that recipient.

If you sign in from another browser or device, that device can publish its own key too. Egroo supports multiple active device keys for one user so encrypted delivery can work across devices.

If you later clear browser storage or move to another device, you may need to regenerate or republish the device key from the app settings before encrypted messages can be decrypted on that device.

## 5. Verify The Setup

Use this quick checklist:

1. `http://localhost:5176/swagger` opens.
2. The Avalonia window opens.
3. You can create an account and sign in.
4. You can create a channel and send a test message.
5. The API console shows no database connection errors.

## Common First-Run Problems

- SQLite startup failure can indicate that the API cannot write to the database directory.
- When starting the API without `Local Debug`, set the JWT and encryption secrets in server configuration.
- If the client cannot connect, check that the API is listening on the port configured by the desktop profile.
- Messages arrive but cannot be decrypted: the device is missing the current private key.
- Docker compose confusion: the root `docker-compose-egroo.yml` is not the recommended first-run path for contributors.

For fixes, see [Troubleshooting](Troubleshooting).

## Where To Go Next

- [Installation](Installation) for full setup options and Docker caveats
- [Configuration](Configuration) for all runtime settings
- [Development Setup](Development-Setup) if you plan to contribute
- [Deployment](Deployment) if you are preparing a hosted environment
