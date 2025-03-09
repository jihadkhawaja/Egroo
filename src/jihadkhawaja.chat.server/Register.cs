using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public enum DatabaseEnum
{
    Postgres,
    SqlServer
}
public static class Register
{
    public static ChatServiceBuilder ChatService { get; private set; } = null!;

    public static ChatServiceBuilder AddChatServices(this IServiceCollection services)
    {
        return ChatService = new ChatServiceBuilder(services);
    }

    public static void UseChatServices(this WebApplication app)
    {
        //auto-migrate database
        if (ChatService.AutoMigrateDatabase)
        {
            using (IServiceScope scope = app.Services.CreateScope())
            {
                DataContext db = scope.ServiceProvider.GetRequiredService<DataContext>();
                db.Database.Migrate();
            }
        }

        //hubs
        app.MapHub<ChatHub>("/chathub", options =>
        {
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
        });
    }
}

public class ChatServiceBuilder
{
    public string? DbConnectionStringKey { get; private set; }
    public DatabaseEnum SelectedDatabase { get; private set; }
    public string? CurrentExecutionAssemblyName { get; private set; }
    public bool AutoMigrateDatabase { get; private set; }

    private readonly IServiceCollection _services;
    private IConfiguration? _configuration;
    private Type? _executionClassType;
    private DatabaseEnum _databaseEnum;
    private bool _autoMigrateDatabase = true;
    private string _dbConnectionStringKey = "DefaultConnection";

    public ChatServiceBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public ChatServiceBuilder WithConfiguration(IConfiguration config)
    {
        _configuration = config;
        return this;
    }

    public ChatServiceBuilder WithExecutionClassType(Type executionClassType)
    {
        _executionClassType = executionClassType;
        return this;
    }

    public ChatServiceBuilder WithDatabase(DatabaseEnum databaseEnum)
    {
        _databaseEnum = databaseEnum;
        return this;
    }

    public ChatServiceBuilder WithAutoMigrateDatabase(bool autoMigrateDatabase)
    {
        _autoMigrateDatabase = autoMigrateDatabase;
        return this;
    }

    public ChatServiceBuilder WithDbConnectionStringKey(string dbConnectionStringKey)
    {
        _dbConnectionStringKey = dbConnectionStringKey;
        return this;
    }

    public IServiceCollection Build()
    {
        DbConnectionStringKey = _dbConnectionStringKey;
        SelectedDatabase = _databaseEnum;
        CurrentExecutionAssemblyName =
            System.Reflection.Assembly.GetAssembly(_executionClassType).GetName().Name;
        AutoMigrateDatabase = _autoMigrateDatabase;

        ConfigureEntityServices(_services);
        ConfigureDatabase(_services);
        ConfigureSignalR(_services);

        return _services;
    }
    private void ConfigureSignalR(IServiceCollection services)
    {
        services.AddSignalR();
    }
    private void ConfigureDatabase(IServiceCollection services)
    {
        services.AddDbContext<DataContext>();
    }
    private void ConfigureEntityServices(IServiceCollection services)
    {
        services.AddScoped<IEntity<User>, EntityService<User>>();
        services.AddScoped<IEntity<UserFriend>, EntityService<UserFriend>>();
        services.AddScoped<IEntity<Channel>, EntityService<Channel>>();
        services.AddScoped<IEntity<ChannelUser>, EntityService<ChannelUser>>();
        services.AddScoped<IEntity<Message>, EntityService<Message>>();
        services.AddScoped<IEntity<UserPendingMessage>, EntityService<UserPendingMessage>>();
    }
}
