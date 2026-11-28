using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Egroo.Server.Database;

internal sealed class PostgresDataContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
            .Options;
        return new DataContext(options);
    }
}

internal sealed class SqliteDataContextFactory : IDesignTimeDbContextFactory<SqliteDataContext>
{
    public SqliteDataContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var options = new DbContextOptionsBuilder<SqliteDataContext>()
            .UseSqlite(ServerDatabase.GetSqliteConnection(builder))
            .Options;
        return new SqliteDataContext(options);
    }
}
