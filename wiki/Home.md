# Egroo Wiki

This wiki is organized for two audiences:

- people trying to run Egroo for the first time
- contributors who need an accurate view of the current .NET solution and setup flow

Egroo is a self-hosted real-time chat platform built with .NET 10, Blazor Auto, ASP.NET Core, SignalR, and PostgreSQL.

## Read This First

If you only want the shortest path to a working local environment:

1. Install .NET 10 SDK and PostgreSQL.
2. Update `src/Egroo.Server/appsettings.Development.json` with a valid connection string.
3. Run `dotnet watch --project src/Egroo.Server/Egroo.Server.csproj`.
4. Run `dotnet watch --project src/Egroo/Egroo/Egroo.csproj`.
5. Open `http://localhost:5068`.

If you need more detail, continue with the pages below.

## Documentation Map

| Page | Use it for |
|---|---|
| [Getting Started](Getting-Started) | First successful local run with the fewest decisions |
| [Installation](Installation) | Full setup options, including Docker caveats and environment planning |
| [Configuration](Configuration) | Database, JWT, encryption, client URL, CORS, and environment overrides |
| [Development Setup](Development-Setup) | Contributor workflow, build, tests, migrations, and project layout |
| [Deployment](Deployment) | Production planning, reverse proxy, Docker image usage, and operational caveats |
| [Architecture](Architecture) | System design, package boundaries, and runtime behavior |
| [API Documentation](API-Documentation) | REST endpoints and SignalR hub surface |
| [Troubleshooting](Troubleshooting) | Common startup, database, Docker, and SignalR issues |

## What You Need Before Running Egroo

- .NET 10 SDK
- PostgreSQL
- Two local terminals for the API and web host
- A browser that supports modern WebSockets and WebAssembly

Optional tools:

- Docker Desktop for container-based workflows
- Visual Studio 2022 or Visual Studio Code
- pgAdmin, Azure Data Studio, or another PostgreSQL client

## Current Local Runtime Shape

Egroo is not a single executable. In development it runs as separate pieces:

- `src/Egroo.Server` is the API and SignalR backend on `http://localhost:5175`
- `src/Egroo/Egroo` is the Blazor host on `http://localhost:5068`
- `src/Egroo.UI/Constants/Source.cs` points the UI to the API base URL

Database migrations are applied automatically when the API starts.

## Important Notes Before You Choose Docker

The repository contains Docker assets, but they serve different purposes:

- `docker-compose-egroo.yml` expects prebuilt images and an external Docker network named `internal`
- `src/Egroo.Server/docker-compose.yaml` is a simpler API plus PostgreSQL development stack
- the repository does not currently ship a one-command full local stack that provisions web, API, and PostgreSQL together for first-time contributors

For a first local run, manual .NET plus PostgreSQL setup is the most predictable path.

## Community

- GitHub issues: [Report bugs or request features](https://github.com/jihadkhawaja/Egroo/issues)
- Discord: [Join the community server](https://discord.gg/9KMAM2RKVC)
- Contribution guide: [docs/CONTRIBUTING.md](https://github.com/jihadkhawaja/Egroo/blob/main/docs/CONTRIBUTING.md)

## License

Egroo is licensed under the [Apache License 2.0](https://github.com/jihadkhawaja/Egroo/blob/main/LICENSE).