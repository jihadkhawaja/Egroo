using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileChat.Server.Database;
using MobileChat.Server.Hubs;
using MobileChat.Server.Interfaces;
using MobileChat.Server.Services;
using MobileChat.Shared.Models;
using Serilog;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

Configurations = builder.Configuration;

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

//signalr
builder.Services.AddSignalR();

//database
builder.Services.AddDbContext<DataContext>();

//auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("Secrets")["Jwt"]));
    options.TokenValidationParameters.ValidateIssuer = false;
    options.TokenValidationParameters.ValidateAudience = false;
    options.TokenValidationParameters.ValidateLifetime = true;
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for our hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/chathub")))
            {
                // Read the token out of the query string
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(policy =>
{
    policy.AddPolicy("CorsPolicy", opt => opt
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

//services
builder.Services.AddScoped<IEntity<User>, EntityService<User>>();
builder.Services.AddScoped<IEntity<UserFriend>, EntityService<UserFriend>>();
builder.Services.AddScoped<IEntity<Channel>, EntityService<Channel>>();
builder.Services.AddScoped<IEntity<ChannelUser>, EntityService<ChannelUser>>();
builder.Services.AddScoped<IEntity<Message>, EntityService<Message>>();

WebApplication app = builder.Build();

//auto-migrate database
using (IServiceScope scope = app.Services.CreateScope())
{
    DataContext db = scope.ServiceProvider.GetRequiredService<DataContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

//hubs
app.MapHub<ChatHub>("/chathub");

app.Run();

public partial class Program
{
    public static ConfigurationManager Configurations { get; set; }
}