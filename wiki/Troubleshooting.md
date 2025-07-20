# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when working with Egroo.

## 🔧 Common Installation Issues

### Database Connection Issues

#### PostgreSQL Connection Refused
**Error:** `Connection refused (localhost:5432)`

**Solutions:**
1. **Check if PostgreSQL is running:**
   ```bash
   # Linux/macOS
   sudo systemctl status postgresql
   brew services list | grep postgresql
   
   # Windows
   net start postgresql-x64-14
   ```

2. **Verify connection parameters:**
   ```bash
   psql -h localhost -U egroo_user -d egroo -p 5432
   ```

3. **Check PostgreSQL configuration:**
   ```bash
   # Edit postgresql.conf
   sudo nano /etc/postgresql/14/main/postgresql.conf
   
   # Ensure these lines are uncommented:
   listen_addresses = 'localhost'
   port = 5432
   ```

4. **Check pg_hba.conf for authentication:**
   ```bash
   sudo nano /etc/postgresql/14/main/pg_hba.conf
   
   # Add or modify this line:
   local   all             all                                     md5
   host    all             all             127.0.0.1/32            md5
   ```

#### Invalid Connection String
**Error:** `Invalid connection string format`

**Solution:**
Verify your connection string format:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=egroo;Username=egroo_user;Password=your_password;Port=5432"
  }
}
```

### .NET Runtime Issues

#### .NET 8 Not Found
**Error:** `The framework 'Microsoft.NETCore.App', version '8.0.0' was not found`

**Solution:**
1. **Install .NET 8 SDK:**
   ```bash
   # Download from https://dotnet.microsoft.com/download/dotnet/8.0
   
   # Verify installation
   dotnet --version
   ```

2. **Check global.json if present:**
   ```json
   {
     "sdk": {
       "version": "8.0.0",
       "rollForward": "latestFeature"
     }
   }
   ```

#### Package Restore Failures
**Error:** `Package restore failed`

**Solutions:**
1. **Clear NuGet cache:**
   ```bash
   dotnet nuget locals all --clear
   dotnet restore --force
   ```

2. **Check NuGet sources:**
   ```bash
   dotnet nuget list source
   
   # Add official source if missing
   dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
   ```

## 🏗️ Build and Compilation Issues

### Missing Dependencies
**Error:** `The type or namespace 'X' could not be found`

**Solutions:**
1. **Restore packages:**
   ```bash
   cd src
   dotnet restore
   ```

2. **Check project references:**
   ```bash
   dotnet list reference
   ```

3. **Clean and rebuild:**
   ```bash
   dotnet clean
   dotnet build
   ```

### Entity Framework Issues

#### Migrations Not Applied
**Error:** `No database provider has been configured for this DbContext`

**Solutions:**
1. **Check connection string in appsettings.json**
2. **Apply migrations:**
   ```bash
   cd src/Egroo.Server
   dotnet ef database update
   ```

3. **Create migration if needed:**
   ```bash
   dotnet ef migrations add InitialCreate
   ```

#### Migration Conflicts
**Error:** `Pending model changes`

**Solutions:**
1. **Reset database (development only):**
   ```bash
   dotnet ef database drop
   dotnet ef database update
   ```

2. **Create new migration:**
   ```bash
   dotnet ef migrations add ResolvePendingChanges
   dotnet ef database update
   ```

## 🚀 Runtime Issues

### Authentication Problems

#### JWT Token Invalid
**Error:** `401 Unauthorized` responses

**Solutions:**
1. **Check JWT secret configuration:**
   ```json
   {
     "Secrets": {
       "Jwt": "your-256-bit-secret-key-here"
     }
   }
   ```

2. **Verify token format:**
   ```javascript
   // Check if token is properly formatted JWT
   const token = localStorage.getItem('authToken');
   console.log('Token:', token);
   
   // Decode JWT (for debugging only)
   const payload = JSON.parse(atob(token.split('.')[1]));
   console.log('Payload:', payload);
   ```

3. **Check token expiration:**
   ```csharp
   // In TokenGenerator.cs, adjust expiration time
   var tokenDescriptor = new SecurityTokenDescriptor
   {
       Expires = DateTime.UtcNow.AddHours(24), // Adjust as needed
       // ...
   };
   ```

#### CORS Issues
**Error:** `CORS policy: No 'Access-Control-Allow-Origin' header`

**Solutions:**
1. **Check allowed origins configuration:**
   ```json
   {
     "Api": {
       "AllowedOrigins": [
         "http://localhost:5174",
         "https://yourdomain.com"
       ]
     }
   }
   ```

2. **For development, allow all origins:**
   ```json
   {
     "Api": {
       "AllowedOrigins": null
     }
   }
   ```

3. **Verify CORS middleware order in Program.cs:**
   ```csharp
   app.UseRouting();
   app.UseCors("CorsPolicy"); // Must be after UseRouting
   app.UseAuthentication();
   app.UseAuthorization();
   ```

### SignalR Connection Issues

#### Connection Failed
**Error:** `Failed to start the connection`

**Solutions:**
1. **Check WebSocket support:**
   ```javascript
   // Test WebSocket support
   if (window.WebSocket) {
       console.log('WebSockets supported');
   } else {
       console.log('WebSockets not supported');
   }
   ```

2. **Configure SignalR fallbacks:**
   ```javascript
   const connection = new signalR.HubConnectionBuilder()
       .withUrl("/chathub", {
           transport: signalR.HttpTransportType.WebSockets | 
                     signalR.HttpTransportType.ServerSentEvents |
                     signalR.HttpTransportType.LongPolling
       })
       .build();
   ```

3. **Check proxy configuration:**
   ```nginx
   # Nginx configuration for SignalR
   location /chathub {
       proxy_pass http://backend;
       proxy_http_version 1.1;
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