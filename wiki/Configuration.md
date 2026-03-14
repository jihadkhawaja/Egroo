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

Required key:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=yourpassword;Database=egroodb;"
  }
}
```

Environment variable form:

```bash
ConnectionStrings__DefaultConnection=Server=localhost;Port=5432;User Id=postgres;Password=yourpassword;Database=egroodb;
```

The API will fail on startup if `DefaultConnection` is missing.

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

`src/Egroo.Server/appsettings.Development.json`:

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

## Client Configuration

### API Base URL

The web client uses `src/Egroo.UI/Constants/Source.cs`:

```csharp
#if DEBUG
public const string ConnectionURL = "http://localhost:5175/";
#else
public const string ConnectionURL = "https://api.egroo.org/";
#endif
```

What this means:

- local development already points to the local API
- a custom production deployment usually needs the release value changed before build and publish

### Hub URL

The SignalR hub URL is derived from the base URL plus the hub name:

```csharp
public const string HubName = "chathub";
public const string HubConnectionURL = ConnectionURL + HubName;
```

## Runtime Behavior That Affects Configuration

- JWT is accepted in the `Authorization` header for REST endpoints
- the SignalR hub also accepts JWT through the `access_token` query string
- rate limiting is applied under the `Api` policy at 100 requests per minute
- the API automatically applies pending EF Core migrations on startup
- the client release build still uses the API base URL compiled into `src/Egroo.UI/Constants/Source.cs`

## Example Production Server Config

```json
{
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
  "Api": {
    "AllowedOrigins": [
      "https://chat.example.com"
    ]
  }
}
```

## Configuration Checklist

Before blaming the code, confirm these first:

1. `DefaultConnection` points to a reachable PostgreSQL instance.
2. `Secrets:Jwt` exists and is long enough.
3. `Encryption:Key` is 32 characters and `Encryption:IV` is 16 characters.
4. The client is pointing at the correct API base URL for the environment you are running.
5. Production origins are explicitly set if the API is not running in debug.
6. If encrypted messages cannot be read, verify the device has the current private key and published key id.

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
   http://localhost:5175/swagger
   ```

3. **Test authentication**:
   ```bash
   curl -X POST http://localhost:5175/api/v1/Auth/signup \
     -H "Content-Type: application/json" \
     -d '{"username":"testuser","password":"TestPass123!"}'
   ```

4. **Check CORS settings** (verify allowed origins accept requests from your client origin).

## 🆘 Troubleshooting Configuration

Common configuration issues and solutions can be found in the [Troubleshooting Guide](Troubleshooting#configuration-issues).