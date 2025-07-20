# Installation Guide

This comprehensive guide covers different installation methods for Egroo.

## 📦 Installation Methods

### Method 1: Docker Compose (Recommended)

Docker Compose provides the easiest way to deploy Egroo with all dependencies.

#### Prerequisites
- [Docker](https://docs.docker.com/get-docker/)
- [Docker Compose](https://docs.docker.com/compose/install/)

#### Steps

1. **Clone the repository**:
   ```bash
   git clone https://github.com/jihadkhawaja/Egroo.git
   cd Egroo/src
   ```

2. **Configure environment variables**:
   ```bash
   # Create a .env file with your configuration
   cat > .env << EOF
   # Database Configuration
   POSTGRES_DB=egroo
   POSTGRES_USER=egroo_user
   POSTGRES_PASSWORD=secure_password_here
   
   # JWT Secret (generate a secure random string)
   JWT_SECRET=your_jwt_secret_key_here
   
   # API Configuration
   API_ALLOWED_ORIGINS=http://localhost:49168,https://yourdomain.com
   EOF
   ```

3. **Start the services**:
   ```bash
   docker-compose -f docker-compose-egroo.yml up -d
   ```

4. **Verify installation**:
   ```bash
   # Check if containers are running
   docker ps
   
   # Check logs
   docker-compose -f docker-compose-egroo.yml logs
   ```

### Method 2: Manual Installation

For development or custom deployments, you can install Egroo manually.

#### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 12+](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)

#### Database Setup

1. **Install PostgreSQL**:
   ```bash
   # Ubuntu/Debian
   sudo apt update
   sudo apt install postgresql postgresql-contrib
   
   # macOS (using Homebrew)
   brew install postgresql
   
   # Windows - Download from https://www.postgresql.org/download/windows/
   ```

2. **Create database and user**:
   ```bash
   sudo -u postgres psql
   ```
   
   ```sql
   CREATE DATABASE egroo;
   CREATE USER egroo_user WITH ENCRYPTED PASSWORD 'your_secure_password';
   GRANT ALL PRIVILEGES ON DATABASE egroo TO egroo_user;
   \q
   ```

#### Application Setup

1. **Clone and build**:
   ```bash
   git clone https://github.com/jihadkhawaja/Egroo.git
   cd Egroo/src
   dotnet restore
   dotnet build
   ```

2. **Configure the API server**:
   
   Create `src/Egroo.Server/appsettings.Production.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=egroo;Username=egroo_user;Password=your_secure_password"
     },
     "Secrets": {
       "Jwt": "your_jwt_secret_key_here"
     },
     "Api": {
       "AllowedOrigins": ["http://localhost:5174", "https://yourdomain.com"]
     },
     "Serilog": {
       "MinimumLevel": {
         "Default": "Information"
       },
       "WriteTo": [
         {
           "Name": "Console"
         },
         {
           "Name": "File",
           "Args": {
             "path": "logs/egroo-.log",
             "rollingInterval": "Day"
           }
         }
       ]
     }
   }
   ```

3. **Configure the web client**:
   
   Create `src/Egroo/Egroo/appsettings.Production.json`:
   ```json
   {
     "ApiUrl": "http://localhost:5175",
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     }
   }
   ```

4. **Run database migrations**:
   ```bash
   cd src/Egroo.Server
   dotnet ef database update
   ```

5. **Start the services**:
   
   Terminal 1 (API Server):
   ```bash
   cd src/Egroo.Server
   dotnet run --environment Production
   ```
   
   Terminal 2 (Web Client):
   ```bash
   cd src/Egroo/Egroo
   dotnet run --environment Production
   ```

### Method 3: Using Pre-built Docker Images

Use the official Docker images for production deployment.

1. **Pull the images**:
   ```bash
   docker pull jihadkhawaja/mobilechat-server-prod:latest
   docker pull jihadkhawaja/mobilechat-wasm-prod:latest
   ```

2. **Create docker-compose.yml**:
   ```yaml
   version: '3.8'
   services:
     egroo-db:
       image: postgres:15
       environment:
         POSTGRES_DB: egroo
         POSTGRES_USER: egroo_user
         POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
       volumes:
         - postgres_data:/var/lib/postgresql/data
       ports:
         - "5432:5432"
   
     egroo-api:
       image: jihadkhawaja/mobilechat-server-prod:latest
       depends_on:
         - egroo-db
       environment:
         - ConnectionStrings__DefaultConnection=Host=egroo-db;Database=egroo;Username=egroo_user;Password=${POSTGRES_PASSWORD}
         - Secrets__Jwt=${JWT_SECRET}
       ports:
         - "5175:8080"
   
     egroo-web:
       image: jihadkhawaja/mobilechat-wasm-prod:latest
       depends_on:
         - egroo-api
       ports:
         - "5174:8080"
   
   volumes:
     postgres_data:
   ```

3. **Set environment variables and run**:
   ```bash
   export POSTGRES_PASSWORD=your_secure_password
   export JWT_SECRET=your_jwt_secret
   docker-compose up -d
   ```

## 🔍 Verification

After installation, verify that Egroo is working correctly:

1. **Access the application**:
   - Web Interface: http://localhost:5174 (or configured port)
   - API: http://localhost:5175 (or configured port)

2. **Check health endpoints**:
   ```bash
   # API health check
   curl http://localhost:5175/health
   
   # Database connection
   curl http://localhost:5175/api/system/status
   ```

3. **Test user registration**:
   - Open the web interface
   - Create a new account
   - Verify you can log in and access chat features

## 🔧 Post-Installation

- [Configure your installation](Configuration) for your specific needs
- [Set up SSL/HTTPS](Deployment#ssl-configuration) for production
- [Configure backup procedures](Deployment#backup-configuration)
- [Set up monitoring](Deployment#monitoring) for production environments

## 🆘 Troubleshooting

If you encounter issues during installation, check the [Troubleshooting Guide](Troubleshooting) for common solutions.