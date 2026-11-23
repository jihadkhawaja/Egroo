# Development Setup

This guide is for contributors who want a repeatable local development environment that matches the current solution layout.

## Tooling Checklist

Required:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/downloads)

Recommended:

- Visual Studio 2022 or Visual Studio Code
- Docker Desktop for database or deployment experiments
- PostgreSQL and a database client when working with that provider

## Solution Layout

| Project | Role |
|---|---|
| `src/Egroo.Avalonia/Desktop` | Avalonia desktop client |
| `src/Egroo.Avalonia/Browser` | Avalonia WebAssembly client |
| `src/Egroo.UI` | Shared component library |
| `src/Egroo.Server` | API, SignalR, EF Core, repositories |
| `src/Egroo.Client` | Client chat services |
| `src/Egroo.Server.SignalR` | SignalR hub implementation |
| `src/Egroo.Contracts` | Shared contracts and models |
| `src/Egroo.Server.Test` | MSTest project |

The main solution file is `src/Egroo.slnx`.

## 1. Clone And Configure

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

The default launch uses SQLite and creates the database and development secrets under `src/Egroo.Server/App_Data/local-debug`. See [Configuration](Configuration) for the optional PostgreSQL setup.

## 2. Build The Solution

From the repository root:

```bash
dotnet build src/Egroo.slnx --configuration Debug
```

## 3. Run The App Locally

Terminal 1:

```bash
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
```

Terminal 2:

```bash
dotnet run --project src/Egroo.Avalonia/Desktop
```

Runtime defaults:

- API: `http://localhost:5176`
- Desktop app: the Avalonia window opens when the client starts
- Swagger: `http://localhost:5176/swagger`

## 4. Run Tests

Run the server tests:

```bash
dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal
```

The test project uses in-memory EF Core storage and temporary SQLite files. It runs without a PostgreSQL instance.

## 5. Working With Migrations

EF Core keeps separate migration histories for PostgreSQL (`DataContext`) and SQLite (`SqliteDataContext`). Both target `src/Egroo.Server`.

Common scripts:

```powershell
.\scripts\add-migration.ps1 "MigrationName"
.\scripts\update-database.ps1
```

The first command creates a migration for each provider. The second updates the configured SQLite database. Add `-Provider PostgreSQL` to update a configured PostgreSQL database. The API applies pending migrations automatically on startup.

## 6. IDE Notes

### Visual Studio

Open `src/Egroo.slnx` and run both startup projects:

- `Egroo.Server`
- `Egroo`

### Visual Studio Code

The workspace already includes build and test tasks for:

- building the full solution
- building the API
- building the Avalonia client
- running the server tests

Open `Egroo.code-workspace` and press **F5** with **Egroo (Desktop + API)** selected to build and debug both processes with SQLite.

## Development Conventions That Affect Setup

- database access lives in repositories in `src/Egroo.Server/Repository`
- minimal API endpoints live under `src/Egroo.Server/API`
- the SignalR hub is mapped at `/chathub`
- CORS is opened in debug builds
- message payloads can be encrypted per recipient and decrypted on the receiving device
- server-side `Encryption:Key` and `Encryption:IV` are still required for protected server records and encrypted transport workflows
- `IConnectionTracker` is in-memory by default, so local development is effectively single-node

## End-To-End Encryption Notes For Contributors

- user profiles can publish `EncryptionPublicKey` and `EncryptionKeyId`
- device private keys stay in client storage
- clearing browser storage or using a different device can put the client out of sync until the encryption key is regenerated
- if you change persisted encryption fields or mappings, add a migration and update the docs in the same change

## Suggested Daily Workflow

1. Pull latest changes.
2. Build the solution.
3. Start the API and Avalonia desktop client.
4. Run tests for the area you changed.
5. Update docs when behavior or setup changes.

## Related Pages

- [Getting Started](Getting-Started)
- [Configuration](Configuration)
- [Deployment](Deployment)
- [Troubleshooting](Troubleshooting)
