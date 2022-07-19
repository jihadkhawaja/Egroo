# MAUI Client with ASP.Net Server

![Xamarin Chat SignalR Icon](docs/icon.png)

|:warning: WARNING|
|:---------------------------|
|Don't use this branch for production|

# Progress
- [ ] Front-End
- [ ] Back-End

# Requirements
- **dotnet 6.0** (Required), you can use [Visual Studio 2022](https://visualstudio.microsoft.com/vs/preview/) or install [dotnet-6.0 sdk and runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) and use your favorite editor like [Visual Studio Code](https://code.visualstudio.com/)
- [PostgreSQL (Npgsql)](https://www.postgresql.org/) or your preferred database engine (Required)
- [pgAdmin](https://www.pgadmin.org/) to view and edit the PostgreSQL database (optinal)

# Usage
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
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;User Id=postgres;Password=yourpassowrd;Database=mobilechatdb;"
  },
  "Api": {
    "BaseUrl": "/api/v1"
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
2. Create appsecrets.json file and add your own Jwt like below, include it in project solution to be created on build.
```
{
  "Secrets": {
    "Jwt": "your Jwt here example: cACLPY7=*Pe5K%?3"
  }
}
```
3. Set your database connection strings in appsettings.json
```
"ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=mobilechatdb;Trusted_Connection=True;",
    "ProductionConnection": "Server=localhost;Database=databasename;User Id=SA;Password=password;"
  }
```
4. Test your database connection, in the Package Manager Console (Ctrl+`)
```
Add-Migration [your migration name]
```
5. Updating the database is automated from the code but you can test it manually
```
Update-Database
```
If your database is setup correctly you should find the database along with your models tables added to it.

---

#### :grey_exclamation: Notice
This project is under heavy refactoring and development, You may contribute once a release is published.
