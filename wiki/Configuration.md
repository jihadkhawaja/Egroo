# Configuration Guide

This guide covers all configuration options available in Egroo for customizing your installation.

## 📁 Configuration Files

Egroo uses standard ASP.NET Core configuration patterns with `appsettings.json` files:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- Environment variables - Runtime overrides

## 🖥️ Server Configuration

### Database Configuration

#### PostgreSQL (Default)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=yourpassword;Database=egroodb;"
  }
}
```

#### Environment Variable Override
```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Port=5432;User Id=postgres;Password=yourpassword;Database=egroodb;"
```

### JWT Authentication

```json
{
  "Secrets": {
    "Jwt": "your-secret-key-here-not-less-than-32-characters"
  }
}
```

**Important**: The JWT secret must be at least 32 characters long for security.

### Encryption Configuration

```json
{
  "Encryption": {
    "Key": "Your32CharLongEncryptionKeyHere1",
    "IV": "Your16CharLongIV"
  }
}
```

**Important**: 
- Encryption Key must be exactly 32 characters
- IV (Initialization Vector) must be exactly 16 characters
- Use strong, random values in production

**Generate secure keys**:
```bash
# Generate 32-character encryption key
openssl rand -hex 16

# Generate 16-character IV
openssl rand -hex 8

# Generate JWT secret (32+ characters)
openssl rand -base64 32
```

### CORS Configuration

```json
{
  "Api": {
    "AllowedOrigins": [
      "http://localhost:5174",
      "https://yourchat.example.com",
      "https://app.yourdomain.com"
    ]
  }
}
```

For development (allows all origins):
```json
{
  "Api": {
    "AllowedOrigins": null
  }
}
```

### Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/egroo-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### Complete Server Configuration Example

`src/Egroo.Server/appsettings.Production.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/egroo-server-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
        }
      }
    ]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=secure_password;Database=egroodb;"
  },
  "Secrets": {
    "Jwt": "your-secret-key-here-not-less-than-32-characters"
  },
  "Encryption": {
    "Key": "Your32CharLongEncryptionKeyHere1",
    "IV": "Your16CharLongIV"
  },
  "Api": {
    "AllowedOrigins": [
      "https://yourchat.example.com"
    ]
  }
}
```

## 🌐 Client Configuration

### API Connection

`src/Egroo/Egroo/appsettings.json`:
```json
{
  "ApiUrl": "https://api.yourchat.example.com",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### PWA Configuration

PWA settings are configured in `src/Egroo/Egroo/wwwroot/manifest.json`:
```json
{
  "name": "Egroo Chat",
  "short_name": "Egroo",
  "description": "Real-time chat application",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#ffffff",
  "theme_color": "#000000",
  "icons": [
    {
      "src": "icon-192.png",
      "sizes": "192x192",
      "type": "image/png"
    },
    {
      "src": "icon-512.png",
      "sizes": "512x512",
      "type": "image/png"
    }
  ]
}
```

## 🐳 Docker Configuration

### Environment Variables for Docker

Create a `.env` file for Docker Compose:
```bash
# Database
POSTGRES_DB=egroodb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=secure_password_here
POSTGRES_HOST=egroo-db
POSTGRES_PORT=5432

# JWT Configuration
JWT_SECRET=your-secret-key-here-not-less-than-32-characters

# Encryption Configuration
ENCRYPTION_KEY=Your32CharLongEncryptionKeyHere1
ENCRYPTION_IV=Your16CharLongIV

# API Configuration
API_ALLOWED_ORIGINS=https://yourchat.example.com,https://app.yourdomain.com

# Logging Level
LOG_LEVEL=Information
```

### Docker Compose Configuration

`docker-compose.yml`:
```yaml
version: '3.8'

services:
  egroo-db:
    image: postgres:15
    container_name: egroo-database
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql
    ports:
      - "5432:5432"
    networks:
      - egroo-network

  egroo-api:
    image: jihadkhawaja/mobilechat-server-prod:latest
    container_name: egroo-server
    restart: unless-stopped
    depends_on:
      - egroo-db
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=${POSTGRES_HOST:-egroo-db};Port=${POSTGRES_PORT:-5432};User Id=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Database=${POSTGRES_DB};
      - Secrets__Jwt=${JWT_SECRET}
      - Encryption__Key=${ENCRYPTION_KEY}
      - Encryption__IV=${ENCRYPTION_IV}
      - Api__AllowedOrigins__0=${API_ALLOWED_ORIGINS}
      - Logging__LogLevel__Default=${LOG_LEVEL:-Information}
    ports:
      - "5175:8080"
    volumes:
      - ./logs:/app/logs
    networks:
      - egroo-network

  egroo-web:
    image: jihadkhawaja/mobilechat-wasm-prod:latest
    container_name: egroo-client
    restart: unless-stopped
    depends_on:
      - egroo-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    ports:
      - "5174:8080"
    networks:
      - egroo-network

volumes:
  postgres_data:

networks:
  egroo-network:
    driver: bridge
```

## 🔒 Security Configuration

### HTTPS Configuration

For production, always use HTTPS. Configure SSL certificates:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001",
        "Certificate": {
          "Path": "/path/to/certificate.pfx",
          "Password": "certificate_password"
        }
      }
    }
  }
}
```

### Database Security

1. **Use strong passwords**:
   ```bash
   # Generate a secure password
   openssl rand -base64 32
   ```

2. **Limit database access**:
   ```sql
   -- Create a dedicated user with minimal permissions
   CREATE USER egroo_app WITH PASSWORD 'secure_password';
   GRANT CONNECT ON DATABASE egroo TO egroo_app;
   GRANT USAGE ON SCHEMA public TO egroo_app;
   GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO egroo_app;
   ```

### JWT Security

- Use a strong secret key (minimum 256 bits)
- Set appropriate token expiration times
- Consider token refresh strategies for production

## 🔧 Performance Configuration

### Database Performance

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=your_password;Database=egroodb;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;Connection Idle Lifetime=300;"
  }
}
```

### Caching Configuration

For production, consider adding Redis for SignalR backplane:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## 🌍 Environment-Specific Configuration

### Development
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Api": {
    "AllowedOrigins": null
  }
}
```

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

1. **Test database connection**:
   ```bash
   dotnet ef database update --project src/Egroo.Server
   ```

2. **Verify JWT configuration**:
   ```bash
   curl -X POST http://localhost:5175/api/auth/test
   ```

3. **Check CORS settings**:
   ```bash
   curl -H "Origin: https://yourchat.example.com" http://localhost:5175/api/health
   ```

## 🆘 Troubleshooting Configuration

Common configuration issues and solutions can be found in the [Troubleshooting Guide](Troubleshooting#configuration-issues).