# Development Setup

This guide will help you set up a development environment for contributing to Egroo.

## 🎯 Prerequisites

### Required Software
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 12+](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [Visual Studio Code](https://code.visualstudio.com/)

### Recommended Tools
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for containerized development)
- [Azure Data Studio](https://azure.microsoft.com/en-us/products/data-studio/) or [pgAdmin](https://www.pgadmin.org/) (for database management)
- [Postman](https://www.postman.com/) or [Thunder Client](https://www.thunderclient.com/) (for API testing)

## 🛠️ Environment Setup

### 1. Clone the Repository

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

### 2. Database Setup

#### Option A: Local PostgreSQL

1. **Install PostgreSQL**:
   ```bash
   # Windows (using Chocolatey)
   choco install postgresql
   
   # macOS (using Homebrew)
   brew install postgresql
   
   # Ubuntu/Debian
   sudo apt update
   sudo apt install postgresql postgresql-contrib
   ```

2. **Create development database**:
   ```bash
   sudo -u postgres psql
   ```
   
   ```sql
   CREATE DATABASE egroo_dev;
   CREATE USER egroo_dev_user WITH ENCRYPTED PASSWORD 'dev_password';
   GRANT ALL PRIVILEGES ON DATABASE egroo_dev TO egroo_dev_user;
   \q
   ```

#### Option B: Docker PostgreSQL

```bash
docker run --name egroo-postgres \
  -e POSTGRES_DB=egroo_dev \
  -e POSTGRES_USER=egroo_dev_user \
  -e POSTGRES_PASSWORD=dev_password \
  -p 5432:5432 \
  -d postgres:15
```

### 3. Configure Development Settings

Create development configuration files:

#### Server Configuration
`src/Egroo.Server/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=egroo_dev_user;Password=dev_password;Database=egroo_dev;"
  },
  "Secrets": {
    "Jwt": "development-jwt-secret-key-not-for-production-use-only-for-dev"
  },
  "Encryption": {
    "Key": "DevEncryptionKey12345678901234",
    "IV": "DevIV12345678901"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

#### Client Configuration
The client API URL is configured at build time in `src/Egroo.UI/Constants/Source.cs`. In `DEBUG` builds it already points to `http://localhost:5175/` by default — no additional configuration file is needed for development.

### 4. Restore Dependencies and Build

```bash
cd src
dotnet restore
dotnet build
```

> **Note**: Database migrations are applied automatically when the server starts — no manual `dotnet ef database update` is needed for development. EF tools are still useful for creating new migrations:  
> `dotnet tool install --global dotnet-ef`

## 🚀 Running the Application

### Option 1: Using Visual Studio

1. Open `src/Egroo.slnx` in Visual Studio 2022 (17.9+)
2. Set multiple startup projects:
   - Right-click solution → Properties
   - Set `Egroo.Server` and `Egroo` as startup projects
3. Press F5 to start debugging

### Option 2: Using Command Line

#### Terminal 1 - API Server:
```bash
cd src/Egroo.Server
dotnet watch run
```

#### Terminal 2 - Web Client:
```bash
cd src/Egroo/Egroo
dotnet watch run
```

### Option 3: Using Docker Compose (Development)

```bash
docker-compose -f docker-compose-egroo-test.yml up --build
```

## 🧪 Running Tests

### Unit Tests
```bash
cd src
dotnet test
```

### Integration Tests
```bash
# Start the server first
cd src/Egroo.Server
dotnet run &

# Run integration tests
cd ../Egroo.Server.Test
dotnet test
```

### Testing with Postman

Import the Postman collection for API testing:
1. Download [Egroo.postman_collection.json](../postman/Egroo.postman_collection.json)
2. Import into Postman
3. Set environment variables:
   - `baseUrl`: `http://localhost:5175`
   - `token`: (obtained from login endpoint)

## 🔧 Development Tools Configuration

### Visual Studio Code Setup

Install recommended extensions:
```bash
# Install via VS Code command palette
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.blazorwasm-companion
code --install-extension ms-mssql.mssql
code --install-extension bradlc.vscode-tailwindcss
```

Create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch Server",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Egroo.Server/bin/Debug/net10.0/Egroo.Server.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/Egroo.Server",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "name": "Launch Client",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Egroo/Egroo/bin/Debug/net10.0/Egroo.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/Egroo/Egroo",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

Create `.vscode/tasks.json`:
```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/src/Egroo.slnx",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "publish",
      "command": "dotnet",
      "type": "process",
      "args": [
        "publish",
        "${workspaceFolder}/src/Egroo.slnx",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "watch",
      "command": "dotnet",
      "type": "process",
      "args": [
        "watch",
        "run",
        "--project",
        "${workspaceFolder}/src/Egroo.Server"
      ],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

### Git Hooks Setup

Set up pre-commit hooks for code quality:

```bash
# Create .git/hooks/pre-commit
#!/bin/bash
echo "Running pre-commit checks..."

# Format code
dotnet format src/Egroo.slnx --verify-no-changes
if [ $? -ne 0 ]; then
    echo "Code formatting issues found. Run 'dotnet format' to fix."
    exit 1
fi

# Run tests
dotnet test src/Egroo.Server.Test
if [ $? -ne 0 ]; then
    echo "Tests failed. Please fix before committing."
    exit 1
fi

echo "Pre-commit checks passed!"
```

```bash
chmod +x .git/hooks/pre-commit
```

## 🏗️ Project Structure

Understanding the codebase structure:

```
src/
├── Egroo/                          # Blazor Auto Mode App
│   ├── Egroo/                      # Server-side project
│   └── Egroo.Client/              # Client-side project
├── Egroo.Server/                   # API Server
├── Egroo.Server.Test/             # Integration tests
├── Egroo.UI/                      # Shared UI components
├── jihadkhawaja.chat.client/       # Chat client library
├── jihadkhawaja.chat.server/       # Chat server library
└── jihadkhawaja.chat.shared/       # Shared models and types
```

### Key Components

- **SignalR Hub**: Real-time communication (`jihadkhawaja.chat.server/Hubs/`)
- **Minimal API Endpoints**: REST auth routes (`Egroo.Server/API/`)
- **Repositories**: Data access implementations (`Egroo.Server/Repository/`)
- **Shared Models**: Database entities and interfaces (`jihadkhawaja.chat.shared/`)
- **Blazor Components**: UI components (`Egroo.UI/Components/`)
- **Client Services**: SignalR-backed service layer (`jihadkhawaja.chat.client/Services/`)

## 🔍 Debugging

### Server-side Debugging

1. Set breakpoints in your IDE
2. Start debugging mode (F5 in Visual Studio)
3. Make requests to trigger breakpoints

### Client-side Debugging

1. **Browser DevTools**: F12 → Sources tab
2. **Blazor DevTools**: Install browser extension
3. **Console Logging**: Use `Console.WriteLine()` in Blazor components

### Database Debugging

1. **View SQL queries**: Enable EF logging in development
2. **Database profiling**: Use PostgreSQL logs
3. **Query analysis**: Use EXPLAIN ANALYZE for performance

## 📝 Coding Standards

### C# Conventions
- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `dotnet format` to ensure consistent formatting
- Add XML documentation for public APIs

### Blazor Conventions
- Component names should be PascalCase
- Use `@code` blocks for component logic
- Prefer `async`/`await` for data operations

### Database Conventions
- Use Entity Framework migrations for schema changes
- Follow naming conventions: PascalCase for tables and columns
- Add indexes for frequently queried columns

## 🤝 Contributing Workflow

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/your-feature-name`
3. **Make changes and test**
4. **Commit with descriptive messages**
5. **Push to your fork**
6. **Create a Pull Request**

See [Contributing Guide](https://github.com/jihadkhawaja/Egroo/blob/main/docs/CONTRIBUTING.md) for detailed guidelines.

## 🆘 Common Development Issues

### Build Errors
- **NuGet restore issues**: Delete `bin/` and `obj/` folders, run `dotnet restore`
- **Package conflicts**: Check for version mismatches in `.csproj` files

### Database Issues
- **Migration conflicts**: Reset development database and rerun migrations
- **Connection issues**: Verify PostgreSQL is running and credentials are correct

### Runtime Issues
- **SignalR connection failures**: Check CORS configuration
- **Authentication issues**: Verify JWT configuration and token format

For more troubleshooting help, see the [Troubleshooting Guide](Troubleshooting).