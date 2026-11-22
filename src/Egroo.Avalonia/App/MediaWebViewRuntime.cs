using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Egroo.UI.Services;

namespace Egroo.Avalonia;

internal sealed class MediaWebViewRuntime(NativeWebView view, Uri runtimePage) : ClientRuntime
{
    protected override async Task<string> CallAsync(string request)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => CallAsync(request));

        var id = Guid.NewGuid().ToString("N");
        var startupDeadline = DateTime.UtcNow.AddSeconds(15);
        var reloadAt = DateTime.UtcNow.AddSeconds(3);
        try
        {
            while (await EvaluateAsync("typeof globalThis.egrooRuntime") != "object")
            {
                if (DateTime.UtcNow > startupDeadline)
                    throw new TimeoutException("The Egroo API media runtime could not load.");
                if (DateTime.UtcNow >= reloadAt)
                {
                    view.Navigate(runtimePage);
                    reloadAt = DateTime.UtcNow.AddSeconds(3);
                }
                await Task.Delay(100);
            }

            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(request));
            for (var offset = 0; offset < encoded.Length; offset += 16384)
                await EvaluateAsync($"egrooRuntime.append('{id}', '{encoded.Substring(offset, Math.Min(16384, encoded.Length - offset))}')");
            await EvaluateAsync($"void egrooRuntime.start('{id}')");

            var timeout = DateTime.UtcNow.AddMinutes(2);
            var result = new StringBuilder();
            while (DateTime.UtcNow < timeout)
            {
                if (await EvaluateAsync($"egrooRuntime.read('{id}', {result.Length})") is { } part)
                {
                    result.Append(part);
                    if (part.Length < 16384)
                        return result.ToString();
                }
                else
                    await Task.Delay(35);
            }

            throw new TimeoutException("The Egroo media operation timed out.");
        }
        finally
        {
            await EvaluateAsync($"globalThis.egrooRuntime?.clear('{id}')");
        }
    }

    private async Task<string?> EvaluateAsync(string expression)
    {
        var value = await view.InvokeScript(expression);
        if (value?.StartsWith('"') == true)
            return JsonSerializer.Deserialize<string>(value);
        return value is "null" or "undefined" ? null : value;
    }
}
