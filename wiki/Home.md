# Egroo Wiki

This wiki is the practical reference for running, extending, and deploying Egroo.

Egroo is a self-hosted real-time chat platform built with .NET 10, Blazor Auto, ASP.NET Core, SignalR, and PostgreSQL. It supports channel chat, WebRTC voice calls, AI agents, and per-recipient end-to-end encrypted message delivery.

## Read This First

If you want the shortest path to a working local environment:

1. Install .NET 10 SDK and PostgreSQL.
2. Configure `src/Egroo.Server/appsettings.Development.json` with database, JWT, and encryption settings.
3. Run `dotnet watch --project src/Egroo.Server/Egroo.Server.csproj`.
4. Run `dotnet watch --project src/Egroo/Egroo/Egroo.csproj`.
5. Open `http://localhost:5068`, create an account, and sign in.

On first sign-in, the client can generate and publish a device encryption key used for end-to-end message decryption on that device.

## Documentation Map

| Page | Use it for |
|---|---|
| [Getting Started](Getting-Started) | Fastest successful local run |
| [Showcase](Showcase) | Product tour with screenshots of chat, agents, and voice calling |
| [Installation](Installation) | Full setup choices, including Docker tradeoffs |
| [Configuration](Configuration) | Database, JWT, encryption, CORS, logging, and client URL settings |
| [Development Setup](Development-Setup) | Contributor workflow, build, tests, migrations, and repo layout |
| [Deployment](Deployment) | Production planning, reverse proxy requirements, Docker assets, and scale caveats |
| [Architecture](Architecture) | Layer boundaries, runtime behavior, encryption flow, voice calls, and agent execution |
| [API Documentation](API-Documentation) | REST endpoints, SignalR hub methods, and server-to-client events |
| [Troubleshooting](Troubleshooting) | Common startup, database, WebSocket, Docker, and decryption issues |

## What You Need Before Running Egroo

- .NET 10 SDK
- PostgreSQL
- Two local terminals for the API and web host
- A modern browser with WebSocket and WebAssembly support

Optional tools:

- Docker Desktop for container-based workflows
- Visual Studio 2022 or Visual Studio Code
- pgAdmin, Azure Data Studio, or another PostgreSQL client

## Current Local Runtime Shape

Egroo runs as separate development processes:

- `src/Egroo.Server` hosts the API and SignalR backend on `http://localhost:5175`
- `src/Egroo/Egroo` hosts the Blazor web app on `http://localhost:5068`
- `src/Egroo.UI/Constants/Source.cs` controls the UI's API base URL

The API applies pending EF Core migrations on startup.

## Docker Caveat

The repository includes Docker assets, but they target different use cases:

- `docker-compose-egroo.yml` expects prebuilt images and an external Docker network named `internal`
- `src/Egroo.Server/docker-compose.yaml` is a development-oriented API plus PostgreSQL stack
- the repo does not currently provide a one-command full local stack for first-time contributors

For the first local run, manual .NET plus PostgreSQL setup is the most predictable path.

## Community

- GitHub issues: [Report bugs or request features](https://github.com/jihadkhawaja/Egroo/issues)
- Discord: [Join the community server](https://discord.gg/9KMAM2RKVC)
- Contribution guide: [CONTRIBUTING.md](https://github.com/jihadkhawaja/Egroo/blob/main/CONTRIBUTING.md)

## License

Egroo is licensed under the [Apache License 2.0](https://github.com/jihadkhawaja/Egroo/blob/main/LICENSE).