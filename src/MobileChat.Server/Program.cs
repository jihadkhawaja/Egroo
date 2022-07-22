using Microsoft.EntityFrameworkCore;
using MobileChat.Server.Database;
using MobileChat.Server.Hubs;
using MobileChat.Server.Interfaces;
using MobileChat.Server.Services;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddSignalR();

builder.Services.AddDbContext<DataContext>();

//services
builder.Services.AddScoped<IUser, UserService>();
builder.Services.AddScoped<IMessage, MessageService>();
builder.Services.AddScoped<IChannel, ChannelService>();

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