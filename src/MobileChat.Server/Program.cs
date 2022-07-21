using JihadKhawaja.SignalR.Server.Chat.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileChat.Server.Database;
using MobileChat.Server.Helpers;
using MobileChat.Server.Hubs;
using MobileChat.Server.Interfaces;
using MobileChat.Server.Services;
using Serilog;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

//logger
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//import appsettings and secrets json files
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);
//authentication
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
    options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    builder.AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
});

builder.Services.AddSignalR();

builder.Services.AddDbContext<DataContext>();

//services
builder.Services.AddScoped<IUser, UserService>();
builder.Services.AddScoped<IMessage, MessageService>();
builder.Services.AddScoped<IChannel, ChannelService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    db.Database.Migrate();
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

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseRouting();

app.UseCors("CorsPolicy");

//hubs
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<ChatHub>("/chathub");
});

//api
JsonSerializerOptions jsonSerializerOptions = new()
{
    WriteIndented = true,
};
app.MapPost(builder.Configuration.GetSection("Api")["BaseUrl"] + "/signup", async (string username, string password, IUser service) =>
{
    if (await service.UserExist(username))
    {
        return Results.BadRequest();
    }

    User user = new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        Password = password,
        DateCreated = DateTime.UtcNow,
        IsOnline = true
    };

    if (await service.Create(user))
    {
        return Results.Content(JsonSerializer.Serialize(new KeyValuePair<Guid, bool>(user.Id, true)));
    }

    return Results.BadRequest();
}).AllowAnonymous();
app.MapPost(builder.Configuration.GetSection("Api")["BaseUrl"] + "/signin", async (string username, string password, IUser service) =>
{
    if (!await service.UserExist(username))
    {
        return Results.Unauthorized();
    }

    if (!await service.SignIn(username, password))
    {
        return Results.Unauthorized();
    }

    if (PatternMatchHelper.IsEmail(username))
    {
        User registeredUser = await service.ReadByEmail(username);
        //registeredUser.ConnectionId = Context.ConnectionId;
        registeredUser.IsOnline = true;
        await service.Update(registeredUser);

        return Results.Ok(JsonSerializer.Serialize(new KeyValuePair<Guid, bool>(registeredUser.Id, true), jsonSerializerOptions));
    }
    else
    {
        User registeredUser = await service.ReadByUsername(username);
        //registeredUser.ConnectionId = Context.ConnectionId;
        registeredUser.IsOnline = true;
        await service.Update(registeredUser);

        return Results.Ok(JsonSerializer.Serialize(new KeyValuePair<Guid, bool>(registeredUser.Id, true), jsonSerializerOptions));
    }
}).AllowAnonymous();

app.Run();