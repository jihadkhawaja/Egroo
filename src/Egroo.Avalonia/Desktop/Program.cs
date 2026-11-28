using Avalonia;

namespace Egroo.Avalonia.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        App.ApiUrlOverride = Environment.GetEnvironmentVariable("EGROO_API_URL");
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
