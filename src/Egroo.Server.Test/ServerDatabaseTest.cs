using Egroo.Server.Database;
using Egroo.Server.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Egroo.Server.Test;

[TestClass]
public class ServerDatabaseTest
{
    [TestMethod]
    public async Task DefaultDatabaseAppliesMigrationsAndPersistsOrderedTimestamps()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"egroo-{Guid.NewGuid():N}.db");
        var builder = WebApplication.CreateBuilder([]);
        builder.Configuration["Database:Provider"] = null;
        builder.Configuration["ConnectionStrings:DefaultConnection"] = null;
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        builder.Configuration["ConnectionStrings:Sqlite"] = connectionString;
        ServerDatabase.Register(builder);

        var earlier = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var later = earlier.AddTicks(1).ToOffset(TimeSpan.FromHours(-4));
        try
        {
            await using (var app = builder.Build())
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                Assert.IsTrue(db.Database.IsSqlite());
                await db.Database.MigrateAsync();
                CollectionAssert.AreEqual(db.Database.GetMigrations().ToArray(),
                    (await db.Database.GetAppliedMigrationsAsync()).ToArray());
                db.Users.AddRange(
                    new User { Username = "later", Role = "Member", DateCreated = later },
                    new User { Username = "earlier", Role = "Member", DateCreated = earlier });
                await db.SaveChangesAsync();
            }

            var options = new DbContextOptionsBuilder<SqliteDataContext>()
                .UseSqlite(connectionString)
                .Options;
            await using var reopened = new SqliteDataContext(options);
            await reopened.Database.MigrateAsync();
            var users = await reopened.Users.OrderBy(user => user.DateCreated).ToArrayAsync();
            CollectionAssert.AreEqual(new[] { "earlier", "later" }, users.Select(user => user.Username).ToArray());
            Assert.AreEqual(later.UtcTicks, users[1].DateCreated?.UtcTicks);
            Assert.AreEqual(TimeSpan.Zero, users[1].DateCreated?.Offset);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task ExistingPostgreSqlConnectionSelectsPostgreSql()
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.Configuration["Database:Provider"] = null;
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=egroo";
        ServerDatabase.Register(builder);

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.IsTrue(db.Database.IsNpgsql());
    }
}
