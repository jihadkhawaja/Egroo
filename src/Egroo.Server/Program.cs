using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

#region CORS
// In debug mode, allow React development server
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
    policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});
#endregion

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

#region JWT
string jwtKey = builder.Configuration["Secrets:Jwt"] ?? "development-key-that-is-at-least-256-bits-long-for-testing-purposes-only";

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
});
#endregion

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Test endpoints for React integration
app.MapGet("/api/test/health", () => new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    message = "Egroo API is running",
    environment = app.Environment.EnvironmentName
}).AllowAnonymous();

app.MapGet("/api/test/cors", () => new { 
    message = "CORS is working",
    origin = "React frontend can call this endpoint"
}).AllowAnonymous();

// Mock auth endpoints to test the React integration
app.MapPost("/api/v1/Auth/signin", (SignInRequest req) =>
{
    // Mock successful authentication for testing
    return Results.Ok(new AuthResponse
    {
        Token = "mock-jwt-token-for-testing",
        User = new UserDto
        {
            Id = Guid.NewGuid(),
            Username = req.Username
        }
    });
}).AllowAnonymous();

app.MapPost("/api/v1/Auth/signup", (SignUpRequest req) =>
{
    // Mock successful registration for testing
    return Results.Ok(new AuthResponse
    {
        Token = "mock-jwt-token-for-testing",
        User = new UserDto
        {
            Id = Guid.NewGuid(),
            Username = req.Username
        }
    });
}).AllowAnonymous();

Console.WriteLine("Starting Egroo API server on http://localhost:5175");
app.Run();

// DTOs for the mock endpoints
public record SignInRequest(string Username, string Password);
public record SignUpRequest(string Username, string Password);
public record AuthResponse(string Token, UserDto User);
public record UserDto(Guid Id, string Username);