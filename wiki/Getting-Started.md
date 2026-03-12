# Getting Started

This page is the fastest reliable path from a fresh clone to a working local Egroo environment.

## What You Are Starting

For local development, Egroo runs as:

- a PostgreSQL database
- the API and SignalR backend from `src/Egroo.Server`
- the Blazor web host from `src/Egroo/Egroo`

Default development URLs:

- API: `http://localhost:5175`
- Swagger: `http://localhost:5175/swagger`
- Web app: `http://localhost:5068`

## Prerequisites

Install these first:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)

Optional but useful:

- Visual Studio 2022 or Visual Studio Code
- pgAdmin or another PostgreSQL client
- Docker Desktop if you want container-based database or deployment workflows

## 1. Clone The Repository

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

## 2. Create A PostgreSQL Database

Example SQL:

```sql
CREATE DATABASE egroo_local;
CREATE USER egroo_local_user WITH PASSWORD 'change-me';
GRANT ALL PRIVILEGES ON DATABASE egroo_local TO egroo_local_user;
```

If you prefer Docker for the database only:

```bash
docker run --name egroo-postgres \
  -e POSTGRES_DB=egroo_local \
  -e POSTGRES_USER=egroo_local_user \
  -e POSTGRES_PASSWORD=change-me \
  -p 5432:5432 \
  -d postgres:16
```

## 3. Configure The API

Edit `src/Egroo.Server/appsettings.Development.json`.

Minimum example:

```json
{
  "DetailedErrors": true,
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=egroo_local_user;Password=change-me;Database=egroo_local;"
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

Notes:

- `Secrets:Jwt` should be at least 32 characters.
- `Encryption:Key` must be exactly 32 characters.
- `Encryption:IV` must be exactly 16 characters.
- Migrations run automatically when the API starts.

## 4. Start The API

```bash
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
```

What to expect:

- the API listens on `http://localhost:5175`
- Swagger is available in development
- the database is migrated automatically on startup

## 5. Start The Web App

Open a second terminal:

```bash
dotnet watch --project src/Egroo/Egroo/Egroo.csproj
```

Open `http://localhost:5068` in your browser.

The UI connects to the API base URL configured in `src/Egroo.UI/Constants/Source.cs`. In debug builds, that value is already `http://localhost:5175/`.

## 6. Verify The Setup

Use this quick checklist:

1. `http://localhost:5175/swagger` opens.
2. `http://localhost:5068` loads the web app.
3. You can create an account and sign in.
4. The API console shows no database connection errors.

## Common First-Run Problems

- Database connection failure: the connection string or PostgreSQL credentials are wrong.
- API starts but crashes immediately: the JWT or encryption settings are missing.
- Web app loads but chat fails: the API is not running on `http://localhost:5175`.
- Docker compose confusion: the root `docker-compose-egroo.yml` is not the recommended first-run path for contributors.

For fixes, see [Troubleshooting](Troubleshooting).

## Where To Go Next

- [Installation](Installation) for full setup options and Docker caveats
- [Configuration](Configuration) for all runtime settings
- [Development Setup](Development-Setup) if you plan to contribute
- [Deployment](Deployment) if you are preparing a hosted environment