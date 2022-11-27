using jihadkhawaja.mobilechat.server.Database;
using jihadkhawaja.mobilechat.server.Hubs;
using Microsoft.EntityFrameworkCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

Configurations = builder.Configuration;

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
});

//mobile chat service
builder.Services.AddMobileChatServices(builder.Configuration, ServiceCollectionEx.DatabaseEnum.Postgres, typeof(Program));

WebApplication app = builder.Build();

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

//chat hub
//true to use JWT auth
app.UseMobileChatServices(false);

app.Run();

public partial class Program
{
    public static ConfigurationManager Configurations { get; set; }
}