# Deployment Guide

This guide covers various deployment scenarios for Egroo in production environments.

## 🎯 Deployment Overview

Egroo can be deployed in several ways:
- **Docker Compose** (Recommended for small to medium deployments)
- **Kubernetes** (For large-scale deployments)
- **Cloud Platforms** (Azure, AWS, GCP)
- **Traditional hosting** (IIS, Nginx + Kestrel)

## 🐳 Docker Deployment

### Production Docker Compose

Create a production-ready `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  egroo-db:
    image: postgres:15-alpine
    container_name: egroo-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-egroo}
      POSTGRES_USER: ${POSTGRES_USER:-egroo_user}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_INITDB_ARGS: "--auth-host=scram-sha-256"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./postgres/postgresql.conf:/etc/postgresql/postgresql.conf
      - ./postgres/pg_hba.conf:/etc/postgresql/pg_hba.conf
    ports:
      - "127.0.0.1:5432:5432"
    networks:
      - egroo-internal
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-egroo_user} -d ${POSTGRES_DB:-egroo}"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

  egroo-api:
    image: jihadkhawaja/mobilechat-server-prod:latest
    container_name: egroo-server
    restart: unless-stopped
    depends_on:
      egroo-db:
        condition: service_healthy
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=egroo-db;Database=${POSTGRES_DB:-egroo};Username=${POSTGRES_USER:-egroo_user};Password=${POSTGRES_PASSWORD};Port=5432;SSL Mode=Prefer;Trust Server Certificate=true
      - Secrets__Jwt=${JWT_SECRET}
      - Api__AllowedOrigins__0=${FRONTEND_URL}
      - Serilog__MinimumLevel__Default=Warning
      - Serilog__WriteTo__0__Name=Console
      - Serilog__WriteTo__1__Name=File
      - Serilog__WriteTo__1__Args__path=/app/logs/egroo-server-.log
      - Serilog__WriteTo__1__Args__rollingInterval=Day
      - Serilog__WriteTo__1__Args__retainedFileCountLimit=30
    volumes:
      - ./logs/server:/app/logs
      - ./data/uploads:/app/uploads
    ports:
      - "127.0.0.1:5175:8080"
    networks:
      - egroo-internal
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

  egroo-web:
    image: jihadkhawaja/mobilechat-wasm-prod:latest
    container_name: egroo-client
    restart: unless-stopped
    depends_on:
      egroo-api:
        condition: service_healthy
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
    ports:
      - "127.0.0.1:5174:8080"
    networks:
      - egroo-internal
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080 || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

  nginx:
    image: nginx:alpine
    container_name: egroo-nginx
    restart: unless-stopped
    depends_on:
      - egroo-web
      - egroo-api
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - ./logs/nginx:/var/log/nginx
    ports:
      - "80:80"
      - "443:443"
    networks:
      - egroo-internal

volumes:
  postgres_data:
    driver: local

networks:
  egroo-internal:
    driver: bridge
```

### Environment Variables

Create `.env.prod`:
```bash
# Database Configuration
POSTGRES_DB=egroo_prod
POSTGRES_USER=egroo_prod_user
POSTGRES_PASSWORD=very_secure_password_here_use_strong_password

# JWT Configuration (Generate with: openssl rand -base64 64)
JWT_SECRET=your_super_secure_jwt_secret_key_minimum_256_bits

# Application URLs
FRONTEND_URL=https://chat.yourdomain.com
API_URL=https://api.yourdomain.com

# SSL Certificate paths (if using custom certificates)
SSL_CERT_PATH=./ssl/fullchain.pem
SSL_KEY_PATH=./ssl/privkey.pem
```

### Nginx Configuration

Create `nginx/nginx.conf`:
```nginx
events {
    worker_connections 1024;
}

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;
    
    # Logging
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                   '$status $body_bytes_sent "$http_referer" '
                   '"$http_user_agent" "$http_x_forwarded_for"';
    
    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;
    
    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_comp_level 6;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/xml+rss application/json;
    
    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=auth:10m rate=5r/m;
    
    # Upstream servers
    upstream egroo_api {
        server egroo-api:8080;
    }
    
    upstream egroo_web {
        server egroo-web:8080;
    }
    
    # Redirect HTTP to HTTPS
    server {
        listen 80;
        server_name chat.yourdomain.com api.yourdomain.com;
        return 301 https://$server_name$request_uri;
    }
    
    # Web Frontend
    server {
        listen 443 ssl http2;
        server_name chat.yourdomain.com;
        
        ssl_certificate /etc/nginx/ssl/fullchain.pem;
        ssl_certificate_key /etc/nginx/ssl/privkey.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512:ECDHE-RSA-AES256-GCM-SHA384:DHE-RSA-AES256-GCM-SHA384;
        ssl_prefer_server_ciphers off;
        
        # Security headers
        add_header X-Frame-Options DENY;
        add_header X-Content-Type-Options nosniff;
        add_header X-XSS-Protection "1; mode=block";
        add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
        
        location / {
            proxy_pass http://egroo_web;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
    
    # API Backend
    server {
        listen 443 ssl http2;
        server_name api.yourdomain.com;
        
        ssl_certificate /etc/nginx/ssl/fullchain.pem;
        ssl_certificate_key /etc/nginx/ssl/privkey.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512:ECDHE-RSA-AES256-GCM-SHA384:DHE-RSA-AES256-GCM-SHA384;
        ssl_prefer_server_ciphers off;
        
        # Security headers
        add_header X-Frame-Options DENY;
        add_header X-Content-Type-Options nosniff;
        add_header X-XSS-Protection "1; mode=block";
        
        # Rate limiting for auth endpoints
        location /api/auth {
            limit_req zone=auth burst=10 nodelay;
            proxy_pass http://egroo_api;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
        
        # SignalR WebSocket support
        location /chathub {
            proxy_pass http://egroo_api;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
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
        image: jihadkhawaja/mobilechat-server-prod:latest
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
  --image jihadkhawaja/mobilechat-server-prod:latest \
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
      "image": "jihadkhawaja/mobilechat-server-prod:latest",
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

Add health check endpoints to `Program.cs`:
```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)
    .AddCheck("signalr", () => HealthCheckResult.Healthy());

app.MapHealthChecks("/health");
```

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

```bash
# Run migrations in production
docker-compose -f docker-compose.prod.yml exec egroo-api \
  dotnet ef database update --connection "your-connection-string"
```

## 🆘 Troubleshooting Deployment

Common deployment issues and solutions can be found in the [Troubleshooting Guide](Troubleshooting#deployment-issues).