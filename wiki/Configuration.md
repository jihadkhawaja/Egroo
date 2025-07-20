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
    "DefaultConnection": "Host=localhost;Database=egroo;Username=egroo_user;Password=your_password;Port=5432"
  }
}
```

#### Environment Variable Override
```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=egroo;Username=egroo_user;Password=your_password"
```

### JWT Authentication

```json
{
  "Secrets": {
    "Jwt": "your-256-bit-secret-key-here-make-it-very-long-and-secure"
  }
}
```

**Important**: Generate a secure JWT secret:
```bash
# Generate a secure random key
openssl rand -base64 64
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
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/egroo-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
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
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=egroo;Username=egroo_user;Password=secure_password"
  },
  "Secrets": {
    "Jwt": "your-very-long-and-secure-jwt-secret-key-here"
  },
  "Api": {
    "AllowedOrigins": [
      "https://yourchat.example.com"
    ]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/egroo-server-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
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
POSTGRES_DB=egroo
POSTGRES_USER=egroo_user
POSTGRES_PASSWORD=secure_password_here
POSTGRES_HOST=egroo-db
POSTGRES_PORT=5432

# JWT Configuration
JWT_SECRET=your-very-long-and-secure-jwt-secret-key-here

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
      - ConnectionStrings__DefaultConnection=Host=${POSTGRES_HOST:-egroo-db};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Port=${POSTGRES_PORT:-5432}
      - Secrets__Jwt=${JWT_SECRET}
      - Api__AllowedOrigins__0=${API_ALLOWED_ORIGINS}
      - Serilog__MinimumLevel__Default=${LOG_LEVEL:-Information}
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
    "DefaultConnection": "Host=localhost;Database=egroo;Username=egroo_user;Password=your_password;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;Connection Idle Lifetime=300"
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
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
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
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
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
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning"
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