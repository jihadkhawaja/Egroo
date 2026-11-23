using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Database;

internal static class ServerDatabase
{
    internal static void Register(WebApplicationBuilder builder)
    {
        string? postgresConnection = builder.Configuration.GetConnectionString("DefaultConnection");
        string? configuredProvider = builder.Configuration["Database:Provider"];
        // Existing deployments that only set DefaultConnection continue to use PostgreSQL.
        string provider = string.IsNullOrWhiteSpace(configuredProvider)
            ? (string.IsNullOrWhiteSpace(postgresConnection) ? "SQLite" : "PostgreSQL")
            : configuredProvider.Trim();

        switch (provider.ToUpperInvariant())
        {
            case "SQLITE":
                string sqliteConnection = GetSqliteConnection(builder);
                builder.Services.AddDbContext<DataContext, SqliteDataContext>(options => options.UseSqlite(sqliteConnection));
                break;
            case "POSTGRESQL":
                if (string.IsNullOrWhiteSpace(postgresConnection))
                {
                    throw new InvalidOperationException("PostgreSQL requires ConnectionStrings:DefaultConnection.");
                }

                builder.Services.AddDbContext<DataContext>(options => options.UseNpgsql(postgresConnection));
                break;
            default:
                throw new InvalidOperationException("Database:Provider must be SQLite or PostgreSQL.");
        }
    }

    internal static string GetSqliteConnection(WebApplicationBuilder builder)
    {
        var connection = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("Sqlite") ?? "");
        if (connection.DataSource.Length == 0)
        {
            connection.DataSource = Path.Combine("App_Data", "egroo.db");
        }

        if (connection.Mode == SqliteOpenMode.Memory || connection.DataSource == ":memory:")
        {
            throw new InvalidOperationException("Egroo requires a file-backed SQLite database.");
        }

        if (!connection.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            connection.DataSource = Path.GetFullPath(connection.DataSource, builder.Environment.ContentRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
        }

        connection.ForeignKeys ??= true;
        return connection.ToString();
    }
}
