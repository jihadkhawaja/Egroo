using Microsoft.EntityFrameworkCore;
using MobileChat.Server.Database;
using MobileChat.Server.Hubs;
using MobileChat.Server.Interfaces;
using MobileChat.Server.Services;
using MobileChat.Shared.Models;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddSignalR();

builder.Services.AddDbContext<DataContext>();

//services
builder.Services.AddScoped<IEntity<User>, EntityService<User>>();
builder.Services.AddScoped<IEntity<UserFriend>, EntityService<UserFriend>>();
builder.Services.AddScoped<IEntity<Channel>, EntityService<Channel>>();
builder.Services.AddScoped<IEntity<ChannelUser>, EntityService<ChannelUser>>();
builder.Services.AddScoped<IEntity<Message>, EntityService<Message>>();

WebApplication app = builder.Build();

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

//hubs
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<ChatHub>("/chathub");
});

app.Run();