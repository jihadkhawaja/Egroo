using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using Egroo.UI;
using Egroo.UI.Services;

namespace Egroo.Avalonia;

internal sealed class WorkspaceView : UserControl
{
    public void Connect(string? address, ClientRuntime? browserRuntime, Action<NativeWebView, Uri>? configureWebView,
        Func<string, byte[], Task>? exportFile)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var api)
            || api.UserInfo.Length != 0
            || (api.Scheme != "https" && !(api.IsLoopback && api.Scheme == "http")))
        {
            Content = new TextBlock { Text = "Set the Egroo API to an HTTPS address or an HTTP localhost address.", Margin = new Thickness(24) };
            return;
        }

        api = new Uri(api.GetLeftPart(UriPartial.Authority) + "/");
        var surface = new Grid();
        TopLevel.SetAutoSafeAreaPadding(surface, true);
        if (browserRuntime is null)
        {
            var media = new NativeWebView
            {
                Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
                Focusable = false
            };
            var runtimePage = new Uri(api, "runtime/index.html");
            media.EnvironmentRequested += (_, args) =>
            {
                var dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.DoNotVerify);
                var profile = Path.Combine(dataDirectory, "Egroo", "WebView");
                if (args is WindowsWebView2EnvironmentRequestedEventArgs windows)
                    windows.UserDataFolder = profile;
                if (args is GtkWebViewEnvironmentRequestedEventArgs gtk)
                {
                    // WebKitGTK needs the data directory before it initializes persistent storage.
                    Directory.CreateDirectory(profile);
                    gtk.BaseDataDirectory = profile;
                }
                if (args is LinuxWpeWebViewEnvironmentRequestedEventArgs wpe)
                    wpe.DataDirectory = profile;
            };
            media.NavigationStarted += (_, args) => args.Cancel = args.Request != runtimePage
                && args.Request?.Scheme != "about";
            configureWebView?.Invoke(media, api);
            browserRuntime = new MediaWebViewRuntime(media, runtimePage);
            surface.Children.Add(media);
            media.Source = runtimePage;
        }

        surface.Children.Add(new ChatWorkspace(api, browserRuntime, exportFile));
        Content = surface;
    }
}
