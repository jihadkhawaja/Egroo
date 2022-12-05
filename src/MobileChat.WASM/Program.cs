using jihadkhawaja.mobilechat.client.Models;
using jihadkhawaja.mobilechat.client.Services.Extension;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MobileChat.WASM;
using MobileChat.WASM.Services;
using MudBlazor.Services;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
#if DEBUG
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5175"),
    Timeout = TimeSpan.FromMinutes(10)
});
#else
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://www.your-domain.com/"),
    Timeout = TimeSpan.FromMinutes(10)
});
#endif

builder.Services.AddMudServices();

//general services
builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddSingleton<SessionStorage>();
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
        SessionStorage.User = JsonSerializer.Deserialize<User>(await LocalStorageService.GetFromLocalStorage("user"));
    }
    catch { }

    Initialize(HubConnectionURL, SessionStorage.User?.Token);
}

await app.RunAsync();

public partial class Program
{
    // SignalR chat hub name (http://your-web-url/hubName)
    private const string HubName = "chathub";
#if DEBUG
    // Development
    // SignalR Web URL example (http://localhost:2736/ or server IP address with port) where the chat web app is hosted
    public const string HubConnectionURL = "http://localhost:5175/" + HubName;
#else
        // Production
        // SignalR Web URL example (https://www.domain.com/ or server IP address with port) where the chat web app is hosted
        public const string HubConnectionURL = "https://www.your-domain.com/" + HubName;
#endif
    public static CancellationTokenSource ConnectionCancellationTokenSource { get; private set; }

    public static void Initialize(string HubConnectionURL, string Token = "")
    {
        // Initialize client chat signalr service with your server chat hub url
        jihadkhawaja.mobilechat.client.MobileChat.Initialize(HubConnectionURL, Token);
    }

    public static async Task Connect()
    {
        //connect to the server through SignalR chathub
        ConnectionCancellationTokenSource = new();
        if (await jihadkhawaja.mobilechat.client.MobileChat.SignalR.Connect(ConnectionCancellationTokenSource))
        {
            //client connected
        }
    }

    public static async Task Disconnect()
    {
        //disconnect from the server through SignalR chathub
        if (await jihadkhawaja.mobilechat.client.MobileChat.SignalR.Disconnect())
        {
            //client disconnected
        }
    }
}