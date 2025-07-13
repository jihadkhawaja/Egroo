using jihadkhawaja.chat.server.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Egroo.Server
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Configure services to initialize Register.ChatService
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddChatServices()
                .WithConfiguration(configuration)
                .WithExecutionClassType(typeof(DesignTimeDbContextFactory))
                .WithDatabase(DatabaseEnum.Postgres)
                .WithDbConnectionStringKey("DefaultConnection")
                .Build();

            return new DataContext(configuration);
        }
    }
}