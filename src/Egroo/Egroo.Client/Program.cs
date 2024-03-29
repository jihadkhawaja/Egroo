using Egroo.Client;
using Egroo.UI;
using Egroo.UI.Constants;
using Egroo.UI.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(Source.ConnectionURL),
});

ClientModel.MyMudThemeProvider = builder =>
{
    builder.OpenComponent(0, typeof(MyMudThemeProvider));
    builder.CloseComponent();
};

ClientModel.MyMudProvider = builder =>
{
    builder.OpenComponent(0, typeof(MyMudProviders));
    builder.CloseComponent();
};

builder.Services.AddEgrooServices(FrameworkPlatform.WASM);

var app = builder.Build();

await app.RunAsync();