# Configuration

This page documents the settings that matter for getting Egroo running and explains where each one is used.

## Configuration Sources

Egroo follows normal ASP.NET Core configuration precedence:

1. `src/Egroo.Server/appsettings.json`
2. `src/Egroo.Server/appsettings.{Environment}.json`
3. environment variables

The web client also has one build-time constant in `src/Egroo.UI/Constants/Source.cs`.

## Server Settings

### Database Connection

SQLite is the default when neither `Database:Provider` nor the legacy PostgreSQL connection string is configured. The server creates and migrates `App_Data/egroo.db` under its content root. The `Local Debug` profile uses `App_Data/local-debug/egroo.db` and generates its own persistent JWT and encryption settings. To use a custom SQLite file, set `ConnectionStrings:Sqlite` to a file-backed connection string. Relative paths are resolved from the API content root.

```json
{
  "Database": { "Provider": "SQLite" },
  "ConnectionStrings": { "Sqlite": "Data Source=/var/lib/egroo/egroo.db" }
}
```

For PostgreSQL, set `Database:Provider` and provide the connection string.

```json
{
  "Database": { "Provider": "PostgreSQL" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=yourpassword;Database=egroodb;"
  }
}
```

Environment variable form:

```bash
Database__Provider=PostgreSQL
ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Username=postgres;Password=yourpassword;Database=egroodb'
```

Existing deployments that set `ConnectionStrings:DefaultConnection` without a provider continue to use PostgreSQL.

### JWT Secret

Required key:

```json
{
  "Secrets": {
    "Jwt": "your-secret-key-here-not-less-than-32-characters"
  }
}
```

Environment variable form:

```bash
Secrets__Jwt=your-secret-key-here-not-less-than-32-characters
```

The API uses this to sign and validate JWTs for both REST and SignalR authentication.

### Encryption Settings

Required keys:

```json
{
  "Encryption": {
    "Key": "12345678901234567890123456789012",
    "IV": "1234567890123456"
  }
}
```

Rules:

- `Key` must be exactly 32 characters
- `IV` must be exactly 16 characters

These values are used by the server-side `EncryptionService` for protected encrypted records and transport-related encrypted storage.

Important distinction:

- these settings are not the client private keys used for end-to-end message decryption
- end-to-end message keys are generated per device in the client and only the public key is published back to the server

### Recovery email

Recovery email is optional. Configure an authenticated SMTP relay to enable enrollment and password resets. Egroo requires TLS and validates the relay certificate.

| Setting | Purpose |
| --- | --- |
| `RecoveryEmail:Smtp:Host` | SMTP relay hostname |
| `RecoveryEmail:Smtp:Port` | Relay port, usually `587` |
| `RecoveryEmail:Smtp:From` | Sender address the relay allows |
| `RecoveryEmail:Smtp:Username` | Relay credential username |
| `RecoveryEmail:Smtp:Password` | Relay credential password |
| `RecoveryEmail:Smtp:ImplicitTls` | Set to `true` for implicit TLS, usually on port `465`. The default requires STARTTLS. |

Set `RecoveryEmail__Smtp__Password` through your deployment's secret store. Preserve `Encryption:Key` with the database backup to recover verified addresses. The API keeps addresses encrypted at rest and only stores hashes of confirmation and reset codes.

Password resets invalidate previous bearer sessions. The API closes live SignalR sessions on the resetting process as soon as the change is committed. Other server processes recheck idle connections every 10 seconds, and validate the current session revision on each authenticated request or hub call.

### End-To-End Encryption Behavior

Egroo's message delivery can use a device-scoped end-to-end encryption flow:

- the client generates a key pair locally
- the client publishes `EncryptionPublicKey` and `EncryptionKeyId` for the signed-in user
- message payloads are encrypted per recipient using the published public key
- the recipient device decrypts locally with its private key

Operational notes:

- the private key is not meant to live in server configuration
- clearing browser storage can remove the local private key for that device
- if a device key is missing or stale, the client may need to regenerate and republish it from the app settings

### Voice Call Settings

Voice calling works without extra configuration for local experimentation, but production deployments should provide relay-capable ICE servers.

Example configuration:

```json
{
  "VoiceCall": {
    "CloudflareTurn": {
      "TokenId": "your-cloudflare-turn-token-id",
      "ApiToken": "your-cloudflare-turn-api-token"
    },
    "IceServers": [
      {
        "Urls": [
          "stun:stun.cloudflare.com:3478",
          "turn:turn.example.com:3478?transport=udp",
          "turns:turn.example.com:5349?transport=tcp"
        ],
        "Username": "voice-user",
        "Credential": "voice-password"
      }
    ]
  }
}
```

Environment variable form:

```bash
VoiceCall__CloudflareTurn__TokenId=your-cloudflare-turn-token-id
VoiceCall__CloudflareTurn__ApiToken=your-cloudflare-turn-api-token
VoiceCall__IceServers__0__Urls__0=stun:stun.cloudflare.com:3478
VoiceCall__IceServers__0__Urls__1=turn:turn.example.com:3478?transport=udp
VoiceCall__IceServers__0__Urls__2=turns:turn.example.com:5349?transport=tcp
VoiceCall__IceServers__0__Username=voice-user
VoiceCall__IceServers__0__Credential=voice-password
```

Runtime behavior:

- the client requests `/api/v1/Voice/config` before joining a voice call
- the server first tries to generate temporary Cloudflare TURN credentials when `VoiceCall:CloudflareTurn` is configured
- if Cloudflare TURN is unavailable, the server falls back to `VoiceCall:IceServers`
- if nothing is configured, the server returns a default STUN server so local development still works
- on production-like hosts, when TURN is available, the browser forces relay transport instead of trying direct candidates first
- without TURN, calls may work on simple networks but can fail across NAT or firewall boundaries

### Allowed Origins

Optional production setting:

```json
{
  "Api": {
    "AllowedOrigins": [
      "https://chat.example.com",
      "https://app.example.com"
    ]
  }
}
```

Environment variable form:

```bash
Api__AllowedOrigins__0=https://chat.example.com
Api__AllowedOrigins__1=https://app.example.com
```

Important behavior:

- in debug builds, `Program.cs` explicitly disables origin restrictions and allows any origin
- this means `AllowedOrigins` matters most outside debug builds

### Logging

Base logging is configured in `appsettings.json` through ASP.NET Core logging.

Minimal example:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Recommended Development Config

The default `Local Debug` launch profile configures SQLite and generates development secrets. For a local PostgreSQL profile, use `src/Egroo.Server/appsettings.Development.json`.

```json
{
  "Database": { "Provider": "PostgreSQL" },
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

## Client Configuration

### API Base URL

The desktop `Local Debug` profile sets `EGROO_API_URL` to `http://localhost:5176/`. The `Egroo local` profile points to the PostgreSQL API on `http://localhost:5175/`. To publish a client for a custom deployment, pass `-p:EgrooApiUrl=https://api.example.com/`.

### Hub URL

The SignalR hub URL is derived from the base URL plus the hub name:

```csharp
public const string HubName = "chathub";
public const string HubConnectionURL = ConnectionURL + HubName;
```

## Runtime Behavior That Affects Configuration

- JWT is accepted in the `Authorization` header for REST endpoints
- the SignalR hub also accepts JWT through the `access_token` query string
- `GET /api/v1/Voice/config` is anonymous because the browser needs ICE server details before call setup
- rate limiting is applied under the `Api` policy at 100 requests per minute
- the API automatically applies pending EF Core migrations on startup
- the client release build still uses the API base URL compiled into `src/Egroo.UI/Constants/Source.cs`

## Example Production Server Config

```json
{
  "Database": { "Provider": "PostgreSQL" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=db;Port=5432;User Id=egroo;Password=strong-password;Database=egroo;"
  },
  "Secrets": {
    "Jwt": "replace-with-a-real-secret-at-least-32-characters"
  },
  "Encryption": {
    "Key": "replace-with-32-char-secret-value",
    "IV": "replace-with-16-char"
  },
  "VoiceCall": {
    "CloudflareTurn": {
      "TokenId": "replace-with-your-turn-token-id",
      "ApiToken": "replace-with-your-turn-api-token"
    }
  },
  "Api": {
    "AllowedOrigins": [
      "https://chat.example.com"
    ]
  }
}
```

## Configuration Checklist

Before blaming the code, confirm these first:

1. The API can write to the SQLite database directory, or `DefaultConnection` points to a reachable PostgreSQL instance.
2. `Secrets:Jwt` exists and is long enough.
3. `Encryption:Key` is 32 characters and `Encryption:IV` is 16 characters.
4. The client is pointing at the correct API base URL for the environment you are running.
5. Production origins are explicitly set if the API is not running in debug.
6. If encrypted messages cannot be read, verify the device has the current private key and published key id.
7. For production voice calling, verify `VoiceCall` returns at least one relay-capable TURN URL.

### Staging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Api": {
    "AllowedOrigins": [
      "https://staging.yourchat.example.com"
    ]
  }
}
```

### Production
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Api": {
    "AllowedOrigins": [
      "https://yourchat.example.com"
    ]
  }
}
```

## ✅ Configuration Validation

Validate your configuration:

1. **Start the API Server** (database migrations run automatically on startup):
   ```bash
   cd src/Egroo.Server
   dotnet run
   ```

2. **Verify the API is running** (Swagger UI available in development):
   ```
   http://localhost:5176/swagger
   ```

3. **Test authentication**:
   ```bash
   curl -X POST http://localhost:5176/api/v1/Auth/signup \
     -H "Content-Type: application/json" \
     -d '{"username":"testuser","password":"TestPass123!"}'
   ```

4. **Check CORS settings** (verify allowed origins accept requests from your client origin).

## 🆘 Troubleshooting Configuration

Common configuration issues and solutions can be found in the [Troubleshooting Guide](Troubleshooting#configuration-issues).
