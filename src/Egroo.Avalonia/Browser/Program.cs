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
    private static partial Task<string> InvokeJavaScript(string request, string? operationId);

    [JSImport("globalThis.egrooRuntime.cancel")]
    private static partial void CancelJavaScript(string operationId);

    protected override Task<string> CallAsync(string request) => CallAsync(request, CancellationToken.None);

    protected override async Task<string> CallAsync(string request, CancellationToken cancellationToken)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => CallAsync(request, cancellationToken));

        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
            return await InvokeJavaScript(request, null);

        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            return await InvokeJavaScript(request, operationId).WaitAsync(cancellationToken);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                try { CancelJavaScript(operationId); }
                catch (JSException) { }
            }
        }
    }
}
