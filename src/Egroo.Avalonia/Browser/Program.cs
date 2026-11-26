using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Threading;
using Egroo.UI.Services;

namespace Egroo.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal static class Program
{
    private static Task Main(string[] args)
    {
        App.ApiUrlOverride = args.FirstOrDefault();
        App.BrowserRuntime = new BrowserRuntime();
        return AppBuilder.Configure<App>().StartBrowserAppAsync("egroo");
    }
}

[SupportedOSPlatform("browser")]
internal sealed partial class BrowserRuntime : ClientRuntime
{
    [JSImport("globalThis.egrooRuntime.invoke")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    private static partial Task<string> InvokeJavaScript(string request);

    protected override async Task<string> CallAsync(string request)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return await InvokeJavaScript(request);
        return await Dispatcher.UIThread.InvokeAsync(() => InvokeJavaScript(request));
    }
}
