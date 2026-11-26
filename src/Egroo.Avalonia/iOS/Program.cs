using Avalonia.iOS;
using Foundation;
using UIKit;

namespace Egroo.Avalonia.iOS;

internal static class Program
{
    private static void Main(string[] args)
    {
        App.ApiUrlOverride = Environment.GetEnvironmentVariable("EGROO_API_URL");
        App.ExportFile = ShareAsync;
        UIApplication.Main(args, null, typeof(AppDelegate));
    }

    private static async Task ShareAsync(string name, byte[] bytes)
    {
        var window = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows).FirstOrDefault(window => window.IsKeyWindow);
        var host = window?.RootViewController ?? throw new InvalidOperationException("No active window to share the attachment.");
        while (host.PresentedViewController is { } presented)
            host = presented;

        var directory = Directory.CreateTempSubdirectory("egroo-attachment-").FullName;
        var path = Path.Combine(directory, Path.GetFileName(string.IsNullOrWhiteSpace(name) ? "download" : name));
        await File.WriteAllBytesAsync(path, bytes);
        var controller = new UIActivityViewController([NSUrl.FromFilename(path)], null);
        if (controller.PopoverPresentationController is { } popover)
        {
            popover.SourceView = host.View;
            popover.SourceRect = host.View!.Bounds;
        }
        controller.CompletionWithItemsHandler = (_, _, _, _) =>
        {
            controller.Dispose();
            File.Delete(path);
            Directory.Delete(directory);
        };
        host.PresentViewController(controller, true, null);
    }
}

[Register("AppDelegate")]
public sealed class AppDelegate : AvaloniaAppDelegate<App>;
