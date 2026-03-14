# Development Setup

This guide is for contributors who want a repeatable local development environment that matches the current solution layout.

## Tooling Checklist

Required:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)

Recommended:

- Visual Studio 2022 or Visual Studio Code
- Docker Desktop for database or deployment experiments
- pgAdmin, Azure Data Studio, or another PostgreSQL client

## Solution Layout

| Project | Role |
|---|---|
| `src/Egroo/Egroo` | Blazor host application |
| `src/Egroo/Egroo.Client` | Blazor WebAssembly project |
| `src/Egroo.UI` | Shared component library |
| `src/Egroo.Server` | API, SignalR, EF Core, repositories |
| `src/jihadkhawaja.chat.client` | Client chat services |
| `src/jihadkhawaja.chat.server` | SignalR hub implementation |
| `src/jihadkhawaja.chat.shared` | Shared contracts and models |
| `src/Egroo.Server.Test` | MSTest project |

The main solution file is `src/Egroo.slnx`.

## 1. Clone And Configure

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

Create a local PostgreSQL database and then update `src/Egroo.Server/appsettings.Development.json`.

Example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=egroo_dev_user;Password=dev_password;Database=egroo_dev;"
  },
  "Secrets": {
    "Jwt": "development-jwt-secret-at-least-32-characters"
  },
  "Encryption": {
    "Key": "12345678901234567890123456789012",
    "IV": "1234567890123456"
  }
}
```

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
dotnet watch --project src/Egroo/Egroo/Egroo.csproj
```

Runtime defaults:

- API: `http://localhost:5175`
- Web app: `http://localhost:5068`
- Swagger: `http://localhost:5175/swagger`

The UI already targets `http://localhost:5175/` in debug builds through `src/Egroo.UI/Constants/Source.cs`.

## 4. Run Tests

Run the server tests:

```bash
dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal
```

The test project uses an in-memory EF Core setup, so it does not require a live PostgreSQL instance for normal test runs.

## 5. Working With Migrations

Migrations target `src/Egroo.Server`.

Common scripts:

```powershell
.\scripts\add-migration.ps1 "MigrationName"
.\scripts\update-database.ps1
```

Important detail:

- normal local startup already runs `db.Database.MigrateAsync()` automatically
- use migration scripts when you intentionally change the data model

## 6. IDE Notes

### Visual Studio

Open `src/Egroo.slnx` and run both startup projects:

- `Egroo.Server`
- `Egroo`

### Visual Studio Code

The workspace already includes build and test tasks for:

- building the full solution
- building the API
- building the Blazor host
- running the server tests

If you mainly work in VS Code, those tasks are the easiest way to keep the standard commands consistent.

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
- device private keys stay in client storage and are not stored in PostgreSQL
- clearing browser storage or using a different device can put the client out of sync until the encryption key is regenerated
- if you change persisted encryption fields or mappings, add a migration and update the docs in the same change

## Suggested Daily Workflow

1. Pull latest changes.
2. Build the solution.
3. Start API and web host with `dotnet watch`.
4. Run tests for the area you changed.
5. Update docs when behavior or setup changes.

## Related Pages

- [Getting Started](Getting-Started)
- [Configuration](Configuration)
- [Deployment](Deployment)
- [Troubleshooting](Troubleshooting)