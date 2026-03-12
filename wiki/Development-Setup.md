# Development Setup

This guide is for contributors who want a repeatable local development environment that matches the current solution layout.

## Tooling Checklist

Required:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)

Recommended:

- Visual Studio 2022 or Visual Studio Code
- Docker Desktop for database or deployment experiments
- pgAdmin, Azure Data Studio, or another PostgreSQL client

## Solution Layout

| Project | Role |
|---|---|
| `src/Egroo/Egroo` | Blazor host application |
| `src/Egroo/Egroo.Client` | Blazor WebAssembly project |
| `src/Egroo.UI` | Shared component library |
| `src/Egroo.Server` | API, SignalR, EF Core, repositories |
| `src/jihadkhawaja.chat.client` | Client chat services |
| `src/jihadkhawaja.chat.server` | SignalR hub implementation |
| `src/jihadkhawaja.chat.shared` | Shared contracts and models |
| `src/Egroo.Server.Test` | MSTest project |

The main solution file is `src/Egroo.slnx`.

## 1. Clone And Configure

```bash
git clone https://github.com/jihadkhawaja/Egroo.git
cd Egroo
```

Create a local PostgreSQL database and then update `src/Egroo.Server/appsettings.Development.json`.

Example:

```json
{
  "DetailedErrors": true,
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

## 2. Build The Solution

From the repository root:

```bash
dotnet build src/Egroo.slnx --configuration Debug
```

## 3. Run The App Locally

Terminal 1:

```bash
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
```

Terminal 2:

```bash
dotnet watch --project src/Egroo/Egroo/Egroo.csproj
```

Runtime defaults:

- API: `http://localhost:5175`
- Web app: `http://localhost:5068`
- Swagger: `http://localhost:5175/swagger`

The UI already targets `http://localhost:5175/` in debug builds through `src/Egroo.UI/Constants/Source.cs`.

## 4. Run Tests

Run the server tests:

```bash
dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal
```

The test project uses an in-memory EF Core setup, so it does not require a live PostgreSQL instance for normal test runs.

## 5. Working With Migrations

Migrations target `src/Egroo.Server`.

Common scripts:

```powershell
.\scripts\add-migration.ps1 "MigrationName"
.\scripts\update-database.ps1
```

Important detail:

- normal local startup already runs `db.Database.MigrateAsync()` automatically
- use migration scripts when you intentionally change the data model

## 6. IDE Notes

### Visual Studio

Open `src/Egroo.slnx` and run both startup projects:

- `Egroo.Server`
- `Egroo`

### Visual Studio Code

The workspace already includes build and test tasks for:

- building the full solution
- building the API
- building the Blazor host
- running the server tests

If you mainly work in VS Code, those tasks are the easiest way to keep the standard commands consistent.

## Development Conventions That Affect Setup

- database access lives in repositories in `src/Egroo.Server/Repository`
- minimal API endpoints live under `src/Egroo.Server/API`
- the SignalR hub is mapped at `/chathub`
- CORS is opened in debug builds
- message content is stored temporarily and encrypted before delivery
- `IConnectionTracker` is in-memory by default, so local development is effectively single-node

## Suggested Daily Workflow

1. Pull latest changes.
2. Build the solution.
3. Start API and web host with `dotnet watch`.
4. Run tests for the area you changed.
5. Update docs when behavior or setup changes.

## Related Pages

- [Getting Started](Getting-Started)
- [Configuration](Configuration)
- [Deployment](Deployment)
- [Troubleshooting](Troubleshooting)
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