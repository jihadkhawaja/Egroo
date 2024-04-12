using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;

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

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors("CorsPolicyDev");
        }
        else
        {
            app.UseHttpsRedirection();
            app.UseCors("CorsPolicy");
        }

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        //hubs
        app.MapHub<ChatHub>("/chathub", options =>
        {
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
        });
    }
}

public class ChatServiceBuilder
{
    public string DbConnectionStringKey { get; private set; }
    public DatabaseEnum SelectedDatabase { get; private set; }
    public string CurrentExecutionAssemblyName { get; private set; }
    public bool AutoMigrateDatabase { get; private set; }

    private readonly IServiceCollection _services;
    private IConfiguration _configuration;
    private Type _executionClassType;
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

        ConfigureApi(_services);
        ConfigureJwtAuthentication(_services);
        ConfigureSignalR(_services);
        ConfigureDatabase(_services);
        ConfigureAuthorization(_services);
        ConfigureEntityServices(_services);

        return _services;
    }

    private void ConfigureApi(IServiceCollection services)
    {
        //API Rate Limiter
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("Api_Global", options =>
            {
                options.AutoReplenishment = true;
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromMinutes(1);
            });

            options.AddPolicy("Api", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress,
            partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(1)
            }));

            options.AddPolicy("None", httpContext =>
            RateLimitPartition.GetNoLimiter(IPAddress.Loopback));
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(name: "Bearer", securityScheme: new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter the Bearer Authorization string as following: `Bearer Generated-JWT-Token`",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Name = "Bearer",
                                In = ParameterLocation.Header,
                                Reference = new OpenApiReference
                                {
                                    Id = "Bearer",
                                    Type = ReferenceType.SecurityScheme
                                }
                            },
                            new List<string>()
                        }
                    });
        });

        //CORS
        var allowedOrigins = _configuration.GetSection("Api:AllowedOrigins").Get<string[]>();

        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicyDev",
            policy =>
            {
                policy.AllowAnyOrigin();
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
            });
        });

        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                policy =>
                {
                    policy.AllowAnyOrigin();
                    policy.AllowAnyHeader();
                    policy.AllowAnyMethod();
                });
            });
        }
        else
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                policy =>
                {
                    policy.WithOrigins(allowedOrigins);
                    policy.AllowAnyHeader();
                    policy.AllowAnyMethod();
                });
            });
        }
    }
    private void ConfigureJwtAuthentication(IServiceCollection services)
    {
        string jwtKey = _configuration.GetSection("Secrets")["Jwt"]
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
    private void ConfigureSignalR(IServiceCollection services)
    {
        services.AddSignalR();
    }
    private void ConfigureDatabase(IServiceCollection services)
    {
        services.AddDbContext<DataContext>();
    }
    private void ConfigureAuthorization(IServiceCollection services)
    {
        services.AddAuthorization();
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
