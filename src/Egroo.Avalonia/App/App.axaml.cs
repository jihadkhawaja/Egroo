using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Egroo.UI.Services;

namespace Egroo.Avalonia;

public sealed partial class App : Application
{
    public static string? ApiUrlOverride { get; set; }
    public static ClientRuntime? BrowserRuntime { get; set; }
    public static Action<NativeWebView, Uri>? ConfigureWebView { get; set; }
    public static Func<string, byte[], Task>? ExportFile { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var configuredUrl = ApiUrlOverride ?? typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "EgrooApiUrl").Value;
        var view = new WorkspaceView();
        view.Connect(configuredUrl, BrowserRuntime, ConfigureWebView, ExportFile);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            using var icon = AssetLoader.Open(new Uri("avares://Egroo.Avalonia/Assets/egroo.ico"));
            desktop.MainWindow = new Window
            {
                Title = "Egroo",
                Icon = new WindowIcon(icon),
                Width = 1200,
                Height = 800,
                MinWidth = 400,
                MinHeight = 450,
                Content = view
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = view;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
