using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

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

// Add Egroo chat services
builder.Services.AddChatServices()
    .WithConfiguration(builder.Configuration)
    .WithExecutionClassType(typeof(Program))
    .WithDatabase(DatabaseEnum.Postgres)
    .WithAutoMigrateDatabase(true)
    .WithDbConnectionStringKey("DefaultConnection")
    .Build();

WebApplication app = builder.Build();

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

app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Use Egroo chat services
app.UseChatServices();

app.Run();