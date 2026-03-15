using Egroo.Server.API;
using Egroo.Server.Database;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using Egroo.Server.Services;
using jihadkhawaja.chat.server;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        ConfigureInfrastructure(builder);
        ConfigureServerServices(builder.Services);

        WebApplication app = builder.Build();

        await ApplyMigrationsAsync(app);
        ConfigurePipeline(app);
        MapEndpoints(app);

        app.Run();
    }

    private static void ConfigureInfrastructure(WebApplicationBuilder builder)
    {
        ConfigureCors(builder.Services, builder.Configuration);
        ConfigureSwagger(builder.Services);
        ConfigureAuthentication(builder.Services, builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        ConfigureRateLimiting(builder.Services);
        ConfigureDatabase(builder.Services, builder.Configuration);
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
            OnAuthenticationFailed = HandleAuthenticationFailedAsync
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

    private static Task HandleAuthenticationFailedAsync(AuthenticationFailedContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/chathub"))
        {
            return Task.CompletedTask;
        }

        context.NoResult();
        context.Response.StatusCode = 200;
        context.Response.Headers["Content-Type"] = "application/json";
        return context.Response.WriteAsync("{\"error\":\"Unauthenticated access allowed\"}");
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

    private static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new NullReferenceException("DefaultConnection not found");

        services.AddDbContext<DataContext>(options => options.UseNpgsql(connectionString));
    }

    private static void ConfigureServerServices(IServiceCollection services)
    {
        services.AddSingleton(CreateEncryptionService);
        services.AddSingleton<EndToEndEncryptionService>();
        services.AddScoped<IAuth, AuthRepository>();
        services.AddScoped<IUser, UserRepository>();
        services.AddScoped<IChannel, ChannelRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddHttpClient("McpClient");
        services.AddSingleton<ChannelFileStorageService>();
        services.AddSingleton<AgentSkillsService>();
        services.AddSingleton<AgentManagedSkillsService>();
        services.AddSingleton<McpClientService>();
        services.AddSingleton<AgentRuntimeService>();
        services.AddSingleton<AgentChannelService>();
        services.AddSingleton<IAgentChannelResponder>(sp => sp.GetRequiredService<AgentChannelService>());
        services.AddChatHub();
    }

    private static EncryptionService CreateEncryptionService(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var key = configuration.GetSection("Encryption")["Key"];
        var iv = configuration.GetSection("Encryption")["IV"];
        return new EncryptionService(key, iv);
    }

    private static async Task ApplyMigrationsAsync(WebApplication app)
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

        app.UseRouting();
        app.UseRateLimiter();
        app.UseCors("CorsPolicy");
        app.UseAuthentication();
        app.UseAuthorization();
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
        app.MapChannelFiles();
        app.MapVoiceCall();
    }
}