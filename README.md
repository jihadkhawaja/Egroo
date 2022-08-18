# MAUI Client with ASP.Net Server

![Xamarin Chat SignalR Icon](docs/icon.png)

# Documentation
Find everything you need to get started at the [Wiki](https://github.com/jihadkhawaja/MobileChat/wiki/MAUI)

# Requirements
- **dotnet 6.0**, You can use [Visual Studio 2022 17.3 or higher](https://visualstudio.microsoft.com/downloads/) or install [dotnet-6.0 sdk and runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) and use your favorite editor like [Visual Studio Code](https://code.visualstudio.com/) (Required)
- [PostgreSQL (Npgsql)](https://www.postgresql.org/) or your preferred database engine (Required)
- [pgAdmin](https://www.pgadmin.org/) to view and edit the PostgreSQL database (optinal)

# Usage
## Solution
Set multiple project startup by right clicking the solution and then properties and select multiple startup project. Select MobileChat.MAUI and MobileChat.Server to start, Position the MobileChat.Server above MobileChat.MAUI to start before it.

## Client
1. Install the MAUI preview package when installing the Visual Studio 2022 Preview
2. Inside the project apply this command in the developer console in Visual Studio when you first launch the project
```
dotnet restore
``` 

## Server
1. Create **appsettings.json** (Production) and **appsettings.Development.json** (Development) files in the server project root then set the "Build Action" to "Content" and "Copy to Output Directory" to "Copy if newer" for each file.
- Paste these into **appsettings.json** and configure your connection parameters
```
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
        "Name": "File",
        "Args": {
          "path": "logs/log.txt",
          "rollingInterval": "Day"
        }
      },
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}"
        }
      }
    ]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=yourpassowrd;Database=mobilechatdb;"
  }
}
```
- Paste these into **appsettings.Development.json** and configure your connection parameters
```
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=yourpassowrd;Database=mobilechatdb;"
  }
}

```
2. Set your database connection strings in **appsettings.json** and **appsettings.Development.json**
3. Test your database connection, in the Package Manager Console (Ctrl+`)
```
Add-Migration [your migration name]
```
4. Updating the database is automated from the code but you can test it manually
```
Update-Database
```

If your database is setup correctly you should find the database along with your models tables added to it.

If you're database not updating delete your old database and try again otherwise open an issue.

---

### Community
Join the [Discord server](https://discord.gg/9KMAM2RKVC) to get updates, ask questions or send a feedback.

#### Sponsors

<div>
    <a href="https://www.jetbrains.com/" align="right"><img src="https://resources.jetbrains.com/storage/products/company/brand/logos/jb_beam.svg" alt="JetBrains" class="logo-footer" width="72" align="left">
    <a>
    <br/>
        
Special thanks to [JetBrains](https://jb.gg/OpenSourceSupport) for supporting us with open-source licenses for their IDEs. </a>
</div>
