using jihadkhawaja.mobilechat.client;
using jihadkhawaja.mobilechat.client.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MobileChat.UI.Constants;
using MobileChat.UI.Core;
using MobileChat.UI.Interfaces;
using MobileChat.UI.Models;
using MobileChat.UI.Services;
using MobileChat.WASM;
using MudBlazor.Services;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(Source.ConnectionURL),
    Timeout = TimeSpan.FromMinutes(10)
});

builder.Services.AddMudServices();

//general services
builder.Services.AddSingleton<SessionStorage>();
builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddSingleton<ISaveFile, SaveFileService>();
//chat services
builder.Services.AddMobileChatServices();

var app = builder.Build();

//setup cache and try authenticate user
using (IServiceScope scope = app.Services.CreateScope())
{
    LocalStorageService LocalStorageService = scope.ServiceProvider.GetRequiredService<LocalStorageService>();
    SessionStorage SessionStorage = scope.ServiceProvider.GetRequiredService<SessionStorage>();

    try
    {
        SessionStorage.CurrentFrameworkPlatform = FrameworkPlatform.WASM;
        SessionStorage.User = JsonSerializer.Deserialize<User>(await LocalStorageService.GetFromLocalStorage("user"));
    }
    catch { }

    HubInitializer.Initialize(Source.HubConnectionURL, SessionStorage.User?.Token);
    await HubInitializer.Connect();
}

await app.RunAsync();