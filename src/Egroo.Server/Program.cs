using Egroo.Server.API;
using Egroo.Server.Database;
using Egroo.Server.Repository;
using Egroo.Server.Security;
using jihadkhawaja.chat.server;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

#region CORS
var allowedOrigins = builder.Configuration.GetSection("Api:AllowedOrigins").Get<string[]>();

#if DEBUG
allowedOrigins = null;
#endif

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    builder.Services.AddCors(options =>
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
    builder.Services.AddCors(options =>
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(name: "Bearer", securityScheme: new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter the Bearer Authorization string as following: `Bearer Generated-JWT-Token`",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
});
#endregion
#region JWT
string jwtKey = builder.Configuration.GetSection("Secrets")["Jwt"]
            ?? throw new NullReferenceException(nameof(jwtKey));

builder.Services.AddAuthentication(options =>
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
        },
        OnAuthenticationFailed = context =>
        {
            // Allow unauthenticated access to the SignalR hub
            if (context.Request.Path.StartsWithSegments("/chathub"))
            {
                context.NoResult();
                context.Response.StatusCode = 200;
                context.Response.Headers["Content-Type"] = "application/json";
                context.Response.WriteAsync("{\"error\":\"Unauthenticated access allowed\"}");
            }
            return Task.CompletedTask;
        }
    };
});
#endregion

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

#region Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});
#endregion

#region Database
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new NullReferenceException("DefaultConnection not found");

builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(connectionString));
#endregion

#region Egroo Services
// Infrastructure
builder.Services.AddSingleton<EncryptionService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var key = configuration.GetSection("Encryption")["Key"];
    var iv = configuration.GetSection("Encryption")["IV"];
    return new EncryptionService(key, iv);
});
// Repositories (implement shared interfaces)
builder.Services.AddScoped<IAuth, AuthRepository>();
builder.Services.AddScoped<IUser, UserRepository>();
builder.Services.AddScoped<IChannel, ChannelRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

// SignalR hub (from the chat server package)
builder.Services.AddChatHub();
#endregion

WebApplication app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

    app.UseSwagger();
    app.UseSwaggerUI();
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

// Map endpoints
app.MapChatHub();
app.MapAuthentication();

app.Run();