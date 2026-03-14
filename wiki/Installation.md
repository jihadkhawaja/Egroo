# Installation

This page explains the setup options that exist in the repository today and which one to pick.

## Choose The Right Path

| Goal | Recommended path |
|---|---|
| Run Egroo locally for the first time | Manual .NET plus PostgreSQL setup |
| Contribute to the codebase | Manual setup plus the [Development Setup](Development-Setup) guide |
| Run only the backend in containers | `src/Egroo.Server/docker-compose.yaml` |
| Deploy prebuilt web and API images into an existing Docker environment | `docker-compose-egroo.yml` |

For most newcomers, start with the manual path.

## Option 1: Manual Local Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)

### 1. Clone The Repository

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

### 2. Create The Database

```sql
CREATE DATABASE egroo_local;
CREATE USER egroo_local_user WITH PASSWORD 'change-me';
GRANT ALL PRIVILEGES ON DATABASE egroo_local TO egroo_local_user;
```

### 3. Configure The API

Edit `src/Egroo.Server/appsettings.Development.json`.

```json
{
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

### 4. Build The Solution

```bash
dotnet build src/Egroo.slnx --configuration Debug
```

### 5. Start The API And Web App

Terminal 1:

```bash
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
```

Terminal 2:

```bash
dotnet watch --project src/Egroo/Egroo/Egroo.csproj
```

### 6. Open The App

- Web app: `http://localhost:5068`
- API Swagger: `http://localhost:5175/swagger`

## Option 2: Backend-Only Docker Stack

The repository includes `src/Egroo.Server/docker-compose.yaml`, which starts:

- PostgreSQL
- the API container built from `src/Egroo.Server/Dockerfile`

Run it from `src/Egroo.Server`:

```bash
docker compose up --build
```

Notes:

- this compose file is centered on the API, not the full end-user stack
- it exposes the API on `http://localhost:5175`
- it uses a development-style PostgreSQL setup and should be reviewed before using it outside local experiments

## Option 3: Prebuilt Web And API Containers

The root `docker-compose-egroo.yml` uses:

- `jihadkhawaja/egroo-server-prod:latest`
- `jihadkhawaja/egroo-client-prod:latest`

Important caveats:

- it does not provision PostgreSQL
- it assumes an external Docker network named `internal`
- it is better suited to an environment where networking, reverse proxying, and persistent configuration are already managed

If you use it:

1. Create the external network:

```bash
docker network create internal
```

2. Make sure the required application configuration is provided to the containers.

3. Start the stack:

```bash
docker compose -f docker-compose-egroo.yml up -d
```

## Option 4: Build The Docker Images Yourself

From the repository root:

```bash
docker build -f src/Egroo.Server/Dockerfile -t egroo-server .
docker build -f src/Egroo/Egroo/Dockerfile -t egroo-web .
```

This is useful when you want local images without pulling from a registry.

## Installation Checklist

Your setup is ready when all of these are true:

1. PostgreSQL is reachable with the connection string you configured.
2. `http://localhost:5175/swagger` opens in development.
3. `http://localhost:5068` loads the UI.
4. You can sign up and sign in.

## After Installation

- Continue with [Configuration](Configuration) to understand every setting.
- Continue with [Development Setup](Development-Setup) if you plan to change code.
- Continue with [Deployment](Deployment) if you are preparing a hosted environment.