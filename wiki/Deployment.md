# Deployment

This page focuses on what the repository actually supports today and what to check before you put Egroo behind a public URL.

## Production Shape

At minimum, a production deployment needs:

- a PostgreSQL database
- the Egroo API from `src/Egroo.Server`
- the Egroo web host from `src/Egroo/Egroo`
- a reverse proxy or ingress that handles TLS and WebSocket forwarding cleanly

## Important Operational Caveats

Read these before scaling out:

- the SignalR connection tracker is in-memory by default
- horizontal scale requires replacing `IConnectionTracker` with a distributed implementation such as Redis
- the UI release build points to `https://api.egroo.org/` unless you change `src/Egroo.UI/Constants/Source.cs`
- the API expects valid JWT and encryption settings at startup
- database migrations run automatically when the API starts

## Deployment Assets In The Repository

| File | What it is |
|---|---|
| `src/Egroo.Server/Dockerfile` | Builds the API container |
| `src/Egroo/Egroo/Dockerfile` | Builds the Blazor host container |
| `docker-compose-egroo.yml` | Minimal compose for prebuilt web and API images on an external network |
| `src/Egroo.Server/docker-compose.yaml` | Development-oriented API plus PostgreSQL compose file |

## Recommended Production Checklist

1. Provision PostgreSQL separately or as part of your platform.
2. Set `ConnectionStrings__DefaultConnection` for the API.
3. Set `Secrets__Jwt`, `Encryption__Key`, and `Encryption__IV`.
4. Set `Api__AllowedOrigins__*` for the actual web origin.
5. Update the release API URL in `src/Egroo.UI/Constants/Source.cs` before building the web app.
6. Put both services behind HTTPS.
7. Ensure the reverse proxy forwards WebSocket upgrades to `/chathub`.

## Building Release Images

From the repository root:

```bash
docker build -f src/Egroo.Server/Dockerfile -t egroo-server .
docker build -f src/Egroo/Egroo/Dockerfile -t egroo-web .
```

Both Dockerfiles publish .NET 10 applications and expose port `8080` inside the container.

## Using The Root Compose File

`docker-compose-egroo.yml` references prebuilt images:

- `jihadkhawaja/egroo-server-prod:latest`
- `jihadkhawaja/egroo-client-prod:latest`

It also expects a Docker network named `internal`.

Example preparation:

```bash
docker network create internal
docker compose -f docker-compose-egroo.yml up -d
```

Use this only if your environment already handles:

- PostgreSQL
- runtime configuration injection
- reverse proxy and public routing

## Reverse Proxy Requirements

Your proxy must handle these correctly:

- HTTPS termination
- forwarding normal HTTP traffic to the web host and API
- forwarding WebSocket upgrade headers for `/chathub`

If WebSocket upgrades are blocked or downgraded, real-time chat will fail because the hub is configured for WebSockets-only transport.

## Example Environment Variables For The API

```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=db;Port=5432;User Id=egroo;Password=strong-password;Database=egroo;
Secrets__Jwt=replace-with-a-real-secret-at-least-32-characters
Encryption__Key=12345678901234567890123456789012
Encryption__IV=1234567890123456
Api__AllowedOrigins__0=https://chat.example.com
```

## Verification After Deployment

Confirm these after every deployment:

1. The API starts without configuration exceptions.
2. The API can reach PostgreSQL and apply migrations.
3. The web app loads in the browser.
4. Sign-in works.
5. Chat connects and stays connected over SignalR.
6. Browser developer tools show a successful WebSocket connection for `/chathub`.

## When To Avoid A Fancy First Deployment

If you are still learning the project, do not start with Kubernetes or a multi-node layout. First get a single API instance, a single web host, and one PostgreSQL database running correctly. Then harden the environment around that baseline.

```bash
# Update Docker images
docker-compose -f docker-compose.prod.yml pull

# Restart services with zero downtime
docker-compose -f docker-compose.prod.yml up -d --no-deps egroo-api
docker-compose -f docker-compose.prod.yml up -d --no-deps egroo-web
```

### Database Migrations

Database migrations run **automatically on server startup** — no manual migration step is needed in production. The API container will apply any pending migrations before accepting requests.

## 🆘 Troubleshooting Deployment

Common deployment issues and solutions can be found in the [Troubleshooting Guide](Troubleshooting#deployment-issues).