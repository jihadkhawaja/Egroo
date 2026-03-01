# Getting Started

This guide will help you get Egroo up and running quickly in your environment.

## 🏃‍♂️ Quick Start

The fastest way to get started with Egroo is using Docker Compose:

### Using Docker Compose (Recommended)

1. **Clone the repository**:
   ```bash
   git clone https://github.com/jihadkhawaja/Egroo.git
   cd Egroo
   ```

2. **Set up environment variables**:
   ```bash
   # Create environment file
   cp .env.example .env
   
   # Edit the environment variables
   nano .env
   ```

3. **Start the services**:
   ```bash
   docker-compose -f docker-compose-egroo.yml up -d
   ```

4. **Access the application**:
   - The containers join an **external Docker network** called `internal` — exposed ports depend on your reverse proxy (e.g., Nginx) configuration.
   - For development without a proxy, see the [Manual Setup](#manual-setup) section below, where the API runs on `http://localhost:5175` and the web client on `http://localhost:5068`.
   - Swagger UI (development builds only): `http://localhost:5175/swagger`

## 🔧 Manual Setup

If you prefer to run Egroo without Docker:

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/) (version 12 or higher)

### Database Setup
1. **Install PostgreSQL** and create a database:
   ```sql
   CREATE DATABASE egroo;
   CREATE USER egroo_user WITH PASSWORD 'your_password';
   GRANT ALL PRIVILEGES ON DATABASE egroo TO egroo_user;
   ```

2. **Update connection string** in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=egroo;Username=egroo_user;Password=your_password"
     }
   }
   ```

### Running the Application
1. **Start the API Server** (database migrations run automatically on startup):
   ```bash
   cd src/Egroo.Server
   dotnet run
   ```

2. **Start the Web Client** (in a new terminal):
   ```bash
   cd src/Egroo/Egroo
   dotnet run
   ```

## 🎯 What's Next?

- [Configure your setup](Configuration) for production use
- [Set up development environment](Development-Setup) for contributing
- [Deploy to production](Deployment) with various hosting options
- [Explore the API](API-Documentation) to build custom integrations

## 🚨 Common Issues

- **Database connection errors**: Ensure PostgreSQL is running and credentials are correct
- **Port conflicts**: Check if ports 5175 (API) and 5174 (Web) are available
- **CORS issues**: Verify your allowed origins in configuration

For more troubleshooting help, see the [Troubleshooting Guide](Troubleshooting).