# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when working with Egroo.

# Troubleshooting

Use this page when the setup steps are correct on paper but the app still does not start or behave correctly.

## API Will Not Start
# Troubleshooting

Use this page when the setup steps are correct on paper but the app still does not start or behave correctly.

## API Will Not Start

### Symptom

The `Egroo.Server` process exits on startup or throws a configuration exception.

### Checks

1. Make sure `ConnectionStrings:DefaultConnection` exists.
2. Make sure `Secrets:Jwt` exists.
3. Make sure `Encryption:Key` and `Encryption:IV` exist.
4. Confirm the encryption lengths are exact:
   - key: 32 characters
   - IV: 16 characters

## Database Connection Errors

### Symptom

Startup fails with PostgreSQL connection or migration errors.

### Checks

1. Confirm PostgreSQL is running.
2. Confirm the database, user, password, and port in `appsettings.Development.json` are correct.
3. Test the connection with a PostgreSQL client using the same credentials.
4. Confirm the target database already exists.

Example connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=egroo_local_user;Password=change-me;Database=egroo_local;"
  }
}
```

## Web App Loads But Chat Does Not Work

### Symptom

The page opens, but sign-in, channel actions, or real-time chat fail.

### Checks

1. Confirm the API is running on `http://localhost:5175`.
2. Confirm the UI is using the expected API base URL from `src/Egroo.UI/Constants/Source.cs`.
3. Open browser developer tools and check failed requests and WebSocket errors.

In debug builds, the client should already target `http://localhost:5175/`.

## Swagger Does Not Open

### Symptom

`http://localhost:5175/swagger` returns nothing.

### Checks

1. Confirm the API is running in development.
2. Confirm the server actually started and is listening on `http://localhost:5175`.
3. Check the server console for configuration or migration failures.

Swagger is enabled only in development.

## SignalR Connection Fails

### Symptom

The app loads but the real-time connection never stabilizes.

### Checks

1. Confirm the browser or reverse proxy allows WebSocket connections.
2. Confirm `/chathub` is reachable through the same API base URL the UI uses.
3. If you are behind a reverse proxy, verify it forwards `Upgrade` and `Connection` headers.

Important detail:

- Egroo uses WebSockets-only transport for the hub.
- There is no long-polling or server-sent events fallback configured for the supported path.

## Docker Compose Does Not Work

### Symptom

The root compose file starts containers incorrectly or fails immediately.

### Checks

1. Confirm you are using the right compose file for your goal.
2. For `docker-compose-egroo.yml`, confirm the external Docker network `internal` exists.
3. Confirm PostgreSQL is provided separately when using the root compose file.
4. Confirm runtime configuration is injected into the containers.

The root compose file is not a complete first-run developer stack.

## Production Build Talks To The Wrong API

### Symptom

The web app starts, but it tries to connect to `https://api.egroo.org/` instead of your server.

### Fix

Update the release value in `src/Egroo.UI/Constants/Source.cs` before building the web app for your environment.

## Tests Fail After Environment Setup

### Symptom

You can run the app, but test runs fail.

### Checks

1. Run `dotnet build src/Egroo.slnx --configuration Debug` first.
2. Run `dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal`.
3. If package restore is broken, clear NuGet caches and restore again.

```bash
dotnet nuget locals all --clear
dotnet restore src/Egroo.slnx
```

## Still Stuck

When reporting an issue, include:

1. whether you are using manual setup or Docker
2. the exact command you ran
3. the startup error message
4. whether the API, web app, or database is the failing piece