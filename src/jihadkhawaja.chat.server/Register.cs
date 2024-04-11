using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public static class Register
{
    public enum DatabaseEnum
    {
        Postgres,
        SqlServer
    }

    public static IConfiguration? Configuration { get; private set; }
    public static DatabaseEnum SelectedDatabase { get; private set; }
    public static string CurrentExecutionAssemblyName { get; private set; }
    public static string DbConnectionStringKey { get; private set; }
    private static bool AutoMigrateDatabase { get; set; }

    public static IServiceCollection AddChatServices(this IServiceCollection services, IConfiguration config, Type executionClassType,
        DatabaseEnum databaseEnum, bool autoMigrateDatabase = true, string dbConnectionStringKey = "DefaultConnection")
    {
        Configuration = config;
        DbConnectionStringKey = dbConnectionStringKey;
        SelectedDatabase = databaseEnum;
        CurrentExecutionAssemblyName = System.Reflection.Assembly.GetAssembly(executionClassType).GetName().Name;
        AutoMigrateDatabase = autoMigrateDatabase;

        ConfigureJwtAuthentication(services);
        ConfigureSignalR(services);
        ConfigureDatabase(services);
        ConfigureAuthorization(services);
        ConfigureEntityServices(services);

        return services;
    }

    private static void ConfigureJwtAuthentication(IServiceCollection services)
    {
        string jwtKey = Configuration.GetSection("Secrets")["Jwt"]
            ?? throw new NullReferenceException(nameof(jwtKey));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            options.TokenValidationParameters.ValidateIssuer = false;
            options.TokenValidationParameters.ValidateAudience = false;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/chathub")))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
    }

    private static void ConfigureSignalR(IServiceCollection services)
    {
        services.AddSignalR();
    }

    private static void ConfigureDatabase(IServiceCollection services)
    {
        services.AddDbContext<DataContext>();
    }

    private static void ConfigureAuthorization(IServiceCollection services)
    {
        services.AddAuthorization();
    }

    private static void ConfigureEntityServices(IServiceCollection services)
    {
        services.AddScoped<IEntity<User>, EntityService<User>>();
        services.AddScoped<IEntity<UserFriend>, EntityService<UserFriend>>();
        services.AddScoped<IEntity<Channel>, EntityService<Channel>>();
        services.AddScoped<IEntity<ChannelUser>, EntityService<ChannelUser>>();
        services.AddScoped<IEntity<Message>, EntityService<Message>>();
        services.AddScoped<IEntity<UserPendingMessage>, EntityService<UserPendingMessage>>();
    }

    public static void UseMobileChatServices(this WebApplication app)
    {
        //auto-migrate database
        if (AutoMigrateDatabase)
        {
            using (IServiceScope scope = app.Services.CreateScope())
            {
                DataContext db = scope.ServiceProvider.GetRequiredService<DataContext>();
                db.Database.Migrate();
            }
        }

        app.UseAuthentication();
        app.UseAuthorization();

        //hubs
        app.MapHub<ChatHub>("/chathub", options =>
        {
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
        });
    }
}
