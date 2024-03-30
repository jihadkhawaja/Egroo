using Egroo.Client;
using Egroo.Components;
using Egroo.UI;
using Egroo.UI.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

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

builder.Services.AddEgrooServices(FrameworkPlatform.SERVER);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Egroo.Client._Imports).Assembly);

app.Run();