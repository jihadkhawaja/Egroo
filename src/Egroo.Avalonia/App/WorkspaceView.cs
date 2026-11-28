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
        NearbyRuntimePage? nearbyPage = null;
        ClientRuntime? nearbyRuntime = browserRuntime;
        NativeWebView? nearbyView = null;
        var workspaceRuntime = browserRuntime ?? CreateWebRuntime(new Uri(api, "runtime/index.html"));
        surface.Children.Add(new ChatWorkspace(api, workspaceRuntime, exportFile, GetNearbyRuntime));
        Content = surface;
        DetachedFromVisualTree += (_, _) =>
        {
            if (nearbyView is null)
                return;
            surface.Children.Remove(nearbyView);
            nearbyPage?.Dispose();
            nearbyPage = null;
            nearbyView = null;
            nearbyRuntime = null;
        };

        ClientRuntime GetNearbyRuntime()
        {
            if (nearbyRuntime is not null)
                return nearbyRuntime;

            nearbyPage = new NearbyRuntimePage();
            try
            {
                nearbyRuntime = CreateWebRuntime(nearbyPage.Address,
                    "Could not load the local Nearby runtime on this device.");
                return nearbyRuntime;
            }
            catch
            {
                nearbyPage.Dispose();
                nearbyPage = null;
                throw;
            }
        }

        ClientRuntime CreateWebRuntime(Uri runtimePage, string? unavailableMessage = null)
        {
            var media = new NativeWebView
            {
                Width = 1, Height = 1, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
                Focusable = false
            };
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
            configureWebView?.Invoke(media, runtimePage);
            var webRuntime = unavailableMessage is null
                ? new MediaWebViewRuntime(media, runtimePage)
                : new MediaWebViewRuntime(media, runtimePage, unavailableMessage);
            surface.Children.Insert(0, media);
            media.Source = runtimePage;
            if (unavailableMessage is not null)
                nearbyView = media;
            return webRuntime;
        }
    }
}
