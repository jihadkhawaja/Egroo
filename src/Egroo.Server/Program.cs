using Egroo.Server.Authorization;
using Microsoft.AspNetCore.SignalR;
using Egroo.Server;
using Egroo.Server.API;
using Egroo.Server.Database;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using Egroo.Server.Services;
using Egroo.Server.SignalR;
using Egroo.Contracts.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.RateLimiting;

await Program.MainAsync(args);

[ExcludeFromCodeCoverage]
public partial class Program
{
    public static async Task MainAsync(string[] args)
    {
        if (args.FirstOrDefault() is "setup" or "doctor")
        {
            Environment.ExitCode = await MaintenanceCommands.RunAsync(args[0], args[1..]);
            return;
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        ConfigureInfrastructure(builder);
        ConfigureServerServices(builder.Services, builder.Configuration);

        WebApplication app = builder.Build();

        await ApplyMigrationsAsync(app);
        ConfigurePipeline(app);
        MapEndpoints(app);

        app.Run();
    }

    private static void ConfigureInfrastructure(WebApplicationBuilder builder)
    {
        ConfigureLocalDebug(builder);
        ConfigureCors(builder.Services, builder.Configuration);
        ConfigureSwagger(builder.Services);
        ConfigureAuthentication(builder.Services, builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        ConfigureRateLimiting(builder.Services);
        ServerDatabase.Register(builder);
    }

    internal static void ConfigureLocalDebug(WebApplicationBuilder builder, bool createSecrets = true)
    {
        if (!builder.Configuration.GetValue<bool>("Egroo:LocalDebug"))
        {
            return;
        }

#if DEBUG
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Egroo local debug mode requires the Development environment.");
        }

        const string localDataDirectory = "App_Data/local-debug";
        LocalDebugSecrets.Apply(builder.Configuration, Path.Combine(builder.Environment.ContentRootPath, localDataDirectory), createSecrets);
        builder.Configuration["Database:Provider"] = "SQLite";
        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Sqlite")))
        {
            builder.Configuration["ConnectionStrings:Sqlite"] = $"Data Source={localDataDirectory}/egroo.db";
        }

        builder.WebHost.PreferHostingUrls(false).ConfigureKestrel(options =>
        {
            // Ignore configured endpoints so this profile only listens on loopback.
            options.Configure(new ConfigurationBuilder().Build());
            options.ListenLocalhost(5176);
        });
#else
        throw new InvalidOperationException("Egroo local debug mode requires a Debug build.");
#endif
    }

    private static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = GetAllowedOrigins(configuration);
        services.AddCors(options => options.AddPolicy("CorsPolicy", policy => ConfigureCorsPolicy(policy, allowedOrigins)));
    }

    private static string[]? GetAllowedOrigins(IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Api:AllowedOrigins").Get<string[]>();
#if DEBUG
        return null;
#else
        return allowedOrigins;
#endif
    }

    private static void ConfigureCorsPolicy(CorsPolicyBuilder policy, string[]? allowedOrigins)
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            policy.AllowAnyOrigin();
        }

        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    }

    private static void ConfigureSwagger(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(name: "Bearer", securityScheme: CreateJwtSecurityScheme());
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", null, null),
                    new List<string>()
                }
            });
        });
    }

    private static OpenApiSecurityScheme CreateJwtSecurityScheme()
    {
        return new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter the Bearer Authorization string as following: `Bearer Generated-JWT-Token`",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
    }

    private static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        string jwtKey = configuration.GetSection("Secrets")["Jwt"]
            ?? throw new NullReferenceException("Jwt secret not found");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options => ConfigureJwtBearer(options, jwtKey));
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, string jwtKey)
    {
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = HandleMessageReceivedAsync,
            OnTokenValidated = ValidateAccountSessionAsync
        };
    }

    private static Task HandleMessageReceivedAsync(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken) && IsHubOrFilePath(context.HttpContext.Request.Path))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }

    private static async Task ValidateAccountSessionAsync(TokenValidatedContext context)
    {
        if (!AccountSessions.TryReadSession(context.Principal, out var userId, out var revision)
            || await AccountSessions.GetRevisionAsync(context.HttpContext.RequestServices.GetRequiredService<DataContext>(),
                userId, context.HttpContext.RequestAborted) != revision)
        {
            context.Fail("Your session has expired. Sign in again.");
        }
    }

    private static bool IsHubOrFilePath(PathString path)
    {
        return path.StartsWithSegments("/chathub") || path.StartsWithSegments("/api/v1/ChannelFiles");
    }

    private static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("Api", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 10;
            });
        });
    }

    private static void ConfigureServerServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(CreateEncryptionService);
        services.AddSingleton<EndToEndEncryptionService>();
        services.AddScoped<AuthRepository>();
        services.AddScoped<IAuth>(provider => provider.GetRequiredService<AuthRepository>());
        services.AddScoped<ITwoFactorAuth>(provider => provider.GetRequiredService<AuthRepository>());
        services.AddScoped<IAccountRecovery>(provider => provider.GetRequiredService<AuthRepository>());
        services.AddSingleton<IRecoveryEmailDelivery, SmtpRecoveryEmailDelivery>();
        services.AddSingleton<RecoveryEmailQueue>();
        services.AddHostedService(provider => provider.GetRequiredService<RecoveryEmailQueue>());
        services.AddScoped<IUser, UserRepository>();
        services.AddScoped<IChannel, ChannelRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<AgentTaskRepository>();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient("McpClient");
        services.AddSingleton<ChannelFileStorageService>();
        services.AddSingleton<AgentSkillsService>();
        services.AddSingleton<AgentManagedSkillsService>();
        services.AddSingleton<McpClientService>();
        services.AddSingleton<AgentRuntimeService>();
        if (configuration.GetValue("AgentTasks:WorkerEnabled", true))
        {
            services.AddSingleton<IAgentTaskExecutor>(provider => provider.GetRequiredService<AgentRuntimeService>());
            services.AddHostedService<AgentTaskWorker>();
        }

        services.AddSingleton<AgentChannelService>();
        services.AddSingleton<IAgentChannelResponder>(sp => sp.GetRequiredService<AgentChannelService>());
        services.AddSingleton<AccountSessions>();
        services.AddHostedService(provider => provider.GetRequiredService<AccountSessions>());
        services.AddChatHub(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
            options.AddFilter<AccountSessions>();
        });
    }

    private static EncryptionService CreateEncryptionService(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var key = configuration.GetSection("Encryption")["Key"];
        var iv = configuration.GetSection("Encryption")["IV"];
        return new EncryptionService(key, iv);
    }

    internal static async Task ApplyMigrationsAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            await db.Database.EnsureCreatedAsync();
            return;
        }

        await db.Database.MigrateAsync();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            ConfigureDevelopmentPipeline(app);
        }
        else
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/runtime",
            FileProvider = new EmbeddedFileProvider(typeof(Program).Assembly, "Egroo.Server.Runtime")
        });
        app.UseRouting();
        app.UseRateLimiter();
        app.UseCors("CorsPolicy");
        app.UseAuthentication();
        app.Use(RejectInvalidHubCredentialsAsync);
        app.UseAuthorization();
    }

    private static async Task RejectInvalidHubCredentialsAsync(HttpContext context, RequestDelegate next)
    {
        // Public hub methods accept anonymous callers. A failed bearer credential must not become an anonymous connection.
        if (context.Request.Path.StartsWithSegments("/chathub") && (await context.AuthenticateAsync()).Failure is not null)
        {
            await context.ChallengeAsync();
            return;
        }

        await next(context);
    }

    private static void ConfigureDevelopmentPipeline(WebApplication app)
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapChatHub();
        app.MapAuthentication();
        app.MapAgents();
        app.MapAgentTasks();
        app.MapChannelFiles();
        app.MapVoiceCall();
    }
}
