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
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
            proxy_read_timeout 86400;
        }
        
        # General API endpoints
        location / {
            limit_req zone=api burst=20 nodelay;
            proxy_pass http://egroo_api;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

### Deployment Commands

```bash
# Create directories
mkdir -p logs/{server,nginx} data/uploads nginx/ssl

# Start production deployment
docker-compose -f docker-compose.prod.yml --env-file .env.prod up -d

# Check service status
docker-compose -f docker-compose.prod.yml ps

# View logs
docker-compose -f docker-compose.prod.yml logs -f egroo-api
```

## ☸️ Kubernetes Deployment

### Namespace and ConfigMap

```yaml
# k8s/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: egroo

---
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: egroo-config
  namespace: egroo
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  POSTGRES_DB: "egroo"
  POSTGRES_USER: "egroo_user"
```

### Secrets

```yaml
# k8s/secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: egroo-secrets
  namespace: egroo
type: Opaque
data:
  postgres-password: <base64-encoded-password>
  jwt-secret: <base64-encoded-jwt-secret>
```

### Database Deployment

```yaml
# k8s/postgres.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
  namespace: egroo
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:15-alpine
        env:
        - name: POSTGRES_DB
          valueFrom:
            configMapKeyRef:
              name: egroo-config
              key: POSTGRES_DB
        - name: POSTGRES_USER
          valueFrom:
            configMapKeyRef:
              name: egroo-config
              key: POSTGRES_USER
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: egroo-secrets
              key: postgres-password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
      volumes:
      - name: postgres-storage
        persistentVolumeClaim:
          claimName: postgres-pvc

---
apiVersion: v1
kind: Service
metadata:
  name: postgres-service
  namespace: egroo
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
    targetPort: 5432
```

### Application Deployments

```yaml
# k8s/egroo-api.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: egroo-api
  namespace: egroo
spec:
  replicas: 2
  selector:
    matchLabels:
      app: egroo-api
  template:
    metadata:
      labels:
        app: egroo-api
    spec:
      containers:
      - name: egroo-api
        image: jihadkhawaja/egroo-server-prod:latest
        env:
        - name: ConnectionStrings__DefaultConnection
          value: "Host=postgres-service;Database=egroo;Username=egroo_user;Password=$(POSTGRES_PASSWORD)"
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: egroo-secrets
              key: postgres-password
        - name: Secrets__Jwt
          valueFrom:
            secretKeyRef:
              name: egroo-secrets
              key: jwt-secret
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: egroo-api-service
  namespace: egroo
spec:
  selector:
    app: egroo-api
  ports:
  - port: 80
    targetPort: 8080
```

## ☁️ Cloud Platform Deployment

### Azure Container Instances

```bash
# Create resource group
az group create --name egroo-rg --location eastus

# Create container group
az container create \
  --resource-group egroo-rg \
  --name egroo-app \
  --image jihadkhawaja/egroo-server-prod:latest \
  --dns-name-label egroo-app \
  --ports 80 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="your-connection-string" \
  --secure-environment-variables \
    Secrets__Jwt="your-jwt-secret"
```

### AWS ECS with Fargate

```json
{
  "family": "egroo-task",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "executionRoleArn": "arn:aws:iam::123456789012:role/ecsTaskExecutionRole",
  "containerDefinitions": [
    {
      "name": "egroo-api",
      "image": "jihadkhawaja/egroo-server-prod:latest",
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__DefaultConnection",
          "valueFrom": "arn:aws:ssm:region:123456789012:parameter/egroo/db-connection"
        },
        {
          "name": "Secrets__Jwt",
          "valueFrom": "arn:aws:ssm:region:123456789012:parameter/egroo/jwt-secret"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/egroo",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      }
    }
  ]
}
```

## 🖥️ Traditional Hosting

### IIS Deployment (Windows)

1. **Install IIS and ASP.NET Core Hosting Bundle**
2. **Publish the application**:
   ```bash
   dotnet publish src/Egroo.Server -c Release -o ./publish/server
   dotnet publish src/Egroo/Egroo -c Release -o ./publish/client
   ```

3. **Configure IIS sites**:
   - Create site for API (port 5175)
   - Create site for Web app (port 5174)
   - Configure application pools for .NET Core

### Linux with Systemd

1. **Publish and deploy**:
   ```bash
   dotnet publish src/Egroo.Server -c Release -o /opt/egroo/server
   dotnet publish src/Egroo/Egroo -c Release -o /opt/egroo/client
   ```

2. **Create systemd services**:

   `/etc/systemd/system/egroo-api.service`:
   ```ini
   [Unit]
   Description=Egroo API Server
   After=network.target
   
   [Service]
   Type=notify
   User=egroo
   WorkingDirectory=/opt/egroo/server
   ExecStart=/usr/bin/dotnet Egroo.Server.dll
   Restart=always
   RestartSec=10
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=ASPNETCORE_URLS=http://localhost:5175
   SyslogIdentifier=egroo-api
   
   [Install]
   WantedBy=multi-user.target
   ```

   `/etc/systemd/system/egroo-web.service`:
   ```ini
   [Unit]
   Description=Egroo Web Client
   After=network.target
   
   [Service]
   Type=notify
   User=egroo
   WorkingDirectory=/opt/egroo/client
   ExecStart=/usr/bin/dotnet Egroo.dll
   Restart=always
   RestartSec=10
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=ASPNETCORE_URLS=http://localhost:5174
   SyslogIdentifier=egroo-web
   
   [Install]
   WantedBy=multi-user.target
   ```

3. **Enable and start services**:
   ```bash
   sudo systemctl enable egroo-api egroo-web
   sudo systemctl start egroo-api egroo-web
   ```

## 🔒 SSL Configuration

### Let's Encrypt with Certbot

```bash
# Install certbot
sudo apt install certbot python3-certbot-nginx

# Obtain certificates
sudo certbot --nginx -d chat.yourdomain.com -d api.yourdomain.com

# Auto-renewal (add to crontab)
0 12 * * * /usr/bin/certbot renew --quiet
```

### Custom SSL Certificates

```bash
# Generate self-signed certificate (development only)
openssl req -x509 -newkey rsa:4096 -keyout privkey.pem -out fullchain.pem -days 365 -nodes

# Convert to PFX for .NET
openssl pkcs12 -export -out certificate.pfx -inkey privkey.pem -in fullchain.pem
```

## 📊 Monitoring and Logging

### Health Checks

Health check endpoints are not included in the default build. To add them, register in `Program.cs`:
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString);

app.MapHealthChecks("/health");
```

Then update the Kubernetes liveness/readiness probes or Docker health checks to point to `GET /health`.

### Logging with Serilog

Configure structured logging:
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/egroo/egroo-.log",
          "rollingInterval": "Day"
        }
      },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://elasticsearch:9200"
        }
      }
    ]
  }
}
```

### Monitoring with Prometheus

Add metrics collection:
```csharp
builder.Services.AddMetrics();
app.UseMetrics();
```

## 📦 Backup Configuration

### Database Backup

```bash
#!/bin/bash
# backup-db.sh
BACKUP_DIR="/backups"
DATE=$(date +%Y%m%d_%H%M%S)
DB_NAME="egroo"

pg_dump -h localhost -U egroo_user $DB_NAME | gzip > $BACKUP_DIR/egroo_backup_$DATE.sql.gz

# Keep only last 30 days
find $BACKUP_DIR -name "egroo_backup_*.sql.gz" -mtime +30 -delete
```

### Application Data Backup

```bash
#!/bin/bash
# backup-app.sh
tar -czf /backups/egroo_data_$(date +%Y%m%d).tar.gz \
  /opt/egroo/uploads \
  /opt/egroo/logs \
  /opt/egroo/config
```

## 🔄 Updates and Maintenance

### Rolling Updates

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