# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when working with Egroo.

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

- Egroo uses WebSockets-only transport for the hub
- there is no long-polling or server-sent events fallback configured for the official path

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
       proxy_set_header Upgrade $http_upgrade;
       proxy_set_header Connection "upgrade";
       proxy_set_header Host $host;
       proxy_cache_bypass $http_upgrade;
   }
   ```

#### Authentication with SignalR
**Error:** `Unauthorized access to hub`

**Solutions:**
1. **Include token in connection:**
   ```javascript
   const connection = new signalR.HubConnectionBuilder()
       .withUrl("/chathub", {
           accessTokenFactory: () => localStorage.getItem("authToken")
       })
       .build();
   ```

2. **Check JWT configuration for SignalR:**
   ```csharp
   options.Events = new JwtBearerEvents
   {
       OnMessageReceived = context =>
       {
           var accessToken = context.Request.Query["access_token"];
           var path = context.HttpContext.Request.Path;
           
           if (!string.IsNullOrEmpty(accessToken) && 
               path.StartsWithSegments("/chathub"))
           {
               context.Token = accessToken;
           }
           return Task.CompletedTask;
       }
   };
   ```

## 🐳 Docker Issues

### Container Start Failures

#### Port Already in Use
**Error:** `Port 5432 is already in use`

**Solutions:**
1. **Find process using port:**
   ```bash
   # Linux/macOS
   lsof -i :5432
   
   # Windows
   netstat -ano | findstr :5432
   ```

2. **Use different ports:**
   ```yaml
   services:
     egroo-db:
       ports:
         - "5433:5432"  # Use different host port
   ```

3. **Stop conflicting services:**
   ```bash
   sudo systemctl stop postgresql
   ```

#### Docker Compose Issues
**Error:** `Service 'X' failed to build`

**Solutions:**
1. **Check Docker Compose version:**
   ```bash
   docker-compose --version
   # Should be 2.0+ for this configuration
   ```

2. **Build with verbose output:**
   ```bash
   docker-compose -f docker-compose-egroo.yml up --build -d
   ```

3. **Check logs:**
   ```bash
   docker-compose -f docker-compose-egroo.yml logs egroo-api
   ```

### Container Health Issues

#### Database Not Ready
**Error:** `Connection refused` when API starts

**Solutions:**
1. **Add health checks to docker-compose:**
   ```yaml
   egroo-db:
     healthcheck:
       test: ["CMD-SHELL", "pg_isready -U egroo_user -d egroo"]
       interval: 30s
       timeout: 10s
       retries: 3
   
   egroo-api:
     depends_on:
       egroo-db:
         condition: service_healthy
   ```

2. **Add startup delay:**
   ```bash
   # In container startup script
   sleep 30
   dotnet Egroo.Server.dll
   ```

## 🌐 Network and Connectivity Issues

### Firewall Problems
**Error:** Connection timeouts

**Solutions:**
1. **Check firewall rules:**
   ```bash
   # Linux (UFW)
   sudo ufw allow 5174
   sudo ufw allow 5175
   
   # Linux (iptables)
   sudo iptables -A INPUT -p tcp --dport 5174 -j ACCEPT
   sudo iptables -A INPUT -p tcp --dport 5175 -j ACCEPT
   
   # Windows
   # Open Windows Firewall and add rules for ports 5174, 5175
   ```

2. **Test port connectivity:**
   ```bash
   # Test if port is open
   telnet localhost 5175
   
   # Or use nc
   nc -zv localhost 5175
   ```

### DNS Resolution Issues
**Error:** `Name resolution failed`

**Solutions:**
1. **Use IP addresses instead of hostnames for testing**
2. **Check /etc/hosts file:**
   ```bash
   sudo nano /etc/hosts
   
   # Add entries like:
   127.0.0.1 egroo-api
   127.0.0.1 egroo-web
   ```

## 🎨 Frontend Issues

### Blazor WebAssembly Issues

#### Loading Failures
**Error:** `Failed to find a valid digest in the 'integrity' attribute`

**Solutions:**
1. **Clear browser cache:**
   - Chrome: Ctrl+Shift+R
   - Firefox: Ctrl+F5
   - Safari: Cmd+Shift+R

2. **Disable PWA caching during development:**
   ```csharp
   // In Program.cs (client)
   #if DEBUG
   builder.Services.AddSingleton<IPWAService, NoPWAService>();
   #endif
   ```

3. **Check service worker:**
   ```javascript
   // Unregister service worker
   navigator.serviceWorker.getRegistrations().then(function(registrations) {
       for(let registration of registrations) {
           registration.unregister();
       }
   });
   ```

### JavaScript Interop Issues
**Error:** `JS interop call failed`

**Solutions:**
1. **Check if JavaScript is loaded:**
   ```csharp
   try
   {
       await JSRuntime.InvokeVoidAsync("console.log", "JS interop working");
   }
   catch (Exception ex)
   {
       Console.WriteLine($"JS interop failed: {ex.Message}");
   }
   ```

2. **Add error handling:**
   ```csharp
   public async Task<bool> SafeJSCall(string method, params object[] args)
   {
       try
       {
           await JSRuntime.InvokeVoidAsync(method, args);
           return true;
       }
       catch (JSException)
       {
           return false;
       }
   }
   ```

## 📱 Mobile and PWA Issues

### Installation Problems
**Error:** PWA install banner not showing

**Solutions:**
1. **Ensure HTTPS in production**
2. **Check manifest.json:**
   ```json
   {
     "name": "Egroo Chat",
     "short_name": "Egroo",
     "start_url": "/",
     "display": "standalone",
     "background_color": "#ffffff",
     "theme_color": "#000000"
   }
   ```

3. **Verify service worker registration:**
   ```javascript
   if ('serviceWorker' in navigator) {
       navigator.serviceWorker.register('/sw.js')
           .then(reg => console.log('SW registered', reg))
           .catch(err => console.log('SW failed', err));
   }
   ```

### Push Notification Issues
**Error:** Notifications not working

**Solutions:**
1. **Check permissions:**
   ```javascript
   if (Notification.permission !== 'granted') {
       await Notification.requestPermission();
   }
   ```

2. **Verify VAPID keys configuration**
3. **Test on HTTPS only (PWA requirement)**

## 🔍 Performance Issues

### Slow Database Queries
**Symptoms:** Long response times

**Solutions:**
1. **Enable SQL logging:**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Microsoft.EntityFrameworkCore.Database.Command": "Information"
       }
     }
   }
   ```

2. **Add database indexes:**
   ```csharp
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       modelBuilder.Entity<Message>()
           .HasIndex(m => new { m.ChannelId, m.CreatedAt });
   }
   ```

3. **Use query optimization:**
   ```csharp
   // Instead of
   var messages = await context.Messages
       .Where(m => m.ChannelId == channelId)
       .ToListAsync();
   
   // Use
   var messages = await context.Messages
       .Where(m => m.ChannelId == channelId)
       .AsNoTracking()
       .Take(50)
       .ToListAsync();
   ```

### High Memory Usage
**Symptoms:** Application crashes or slow performance

**Solutions:**
1. **Check for memory leaks:**
   ```csharp
   // Dispose resources properly
   public void Dispose()
   {
       _hubConnection?.DisposeAsync();
       _httpClient?.Dispose();
   }
   ```

2. **Configure garbage collection:**
   ```xml
   <PropertyGroup>
     <ServerGarbageCollection>true</ServerGarbageCollection>
     <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
   </PropertyGroup>
   ```

## 🛠️ Diagnostic Tools

### Logging and Monitoring

#### Enable Detailed Logging
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore.SignalR": "Debug",
        "Microsoft.AspNetCore.Http.Connections": "Debug"
      }
    }
  }
}
```

#### Application Insights
```csharp
services.AddApplicationInsightsTelemetry();

// Custom telemetry
public void TrackException(Exception ex, string context)
{
    _telemetryClient.TrackException(ex, new Dictionary<string, string>
    {
        ["Context"] = context,
        ["UserId"] = GetCurrentUserId()?.ToString()
    });
}
```

### Health Monitoring
```csharp
services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddCheck("signalr-hub", () => HealthCheckResult.Healthy())
    .AddUrlGroup(new Uri("http://localhost:5174"), "frontend");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

## 🆘 Getting Help

### Log Collection
When reporting issues, include:

1. **Application logs:**
   ```bash
   # Server logs
   cat logs/egroo-server-.log
   
   # Docker logs
   docker-compose logs egroo-api
   ```

2. **Browser console logs:**
   - Open DevTools (F12)
   - Check Console and Network tabs
   - Look for errors and failed requests

3. **System information:**
   ```bash
   # .NET version
   dotnet --info
   
   # OS information
   uname -a        # Linux/macOS
   systeminfo      # Windows
   
   # Docker version
   docker --version
   docker-compose --version
   ```

### Community Support
- **GitHub Issues**: [Report bugs and request features](https://github.com/jihadkhawaja/Egroo/issues)
- **Discord Server**: [Join community discussions](https://discord.gg/9KMAM2RKVC)
- **Stack Overflow**: Tag questions with `egroo-chat`

### Professional Support
For enterprise deployments or complex issues:
- Create detailed issue reports with logs
- Provide reproduction steps
- Include environment specifications

## 📋 Common Error Codes

| Error Code | Description | Common Causes | Solution |
|------------|-------------|---------------|----------|
| ERR_CONNECTION_REFUSED | Cannot connect to service | Service not running, wrong port | Check service status and port configuration |
| ERR_CERT_AUTHORITY_INVALID | SSL certificate issues | Self-signed cert, wrong domain | Use proper SSL certificate or disable SSL validation for development |
| ERR_NAME_NOT_RESOLVED | DNS resolution failed | Wrong hostname, network issues | Check hostname configuration and network connectivity |
| 401 Unauthorized | Authentication failed | Invalid token, expired session | Refresh token or re-login |
| 403 Forbidden | Access denied | Insufficient permissions | Check user roles and permissions |
| 500 Internal Server Error | Server error | Application crash, database issues | Check server logs for detailed error information |

This troubleshooting guide covers the most common issues. If you encounter problems not covered here, please check the logs and consider reaching out to the community for support.