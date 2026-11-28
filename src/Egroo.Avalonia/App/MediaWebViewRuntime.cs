using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Egroo.UI.Services;

namespace Egroo.Avalonia;

internal sealed class MediaWebViewRuntime : ClientRuntime
{
    private readonly NativeWebView _view;
    private readonly Uri _runtimePage;
    private readonly string _unavailableMessage;
    private TaskCompletionSource<bool> _navigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _retryNavigation;

    public MediaWebViewRuntime(NativeWebView view, Uri runtimePage,
        string unavailableMessage = "Could not load the Egroo runtime. Check that the API is running, then try again.")
    {
        _view = view;
        _runtimePage = runtimePage;
        _unavailableMessage = unavailableMessage;
        _view.NavigationStarted += (_, args) =>
        {
            if (args.Request == _runtimePage || args.Request?.Scheme == "about")
                ResetNavigation();
        };
        _view.NavigationCompleted += (_, args) =>
        {
            if (args.Request == _runtimePage)
            {
                _retryNavigation = false;
                _navigation.TrySetResult(args.IsSuccess);
            }
        };
        _view.AdapterDestroyed += (_, _) => ResetNavigation();
    }

    protected override async Task<string> CallAsync(string request)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => CallAsync(request));

        var navigation = await WaitForRuntimeAsync();
        var id = Guid.NewGuid().ToString("N");
        try
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(request));
            for (var offset = 0; offset < encoded.Length; offset += 16384)
                await EvaluateAsync($"egrooRuntime.append('{id}', '{encoded.Substring(offset, Math.Min(16384, encoded.Length - offset))}')", navigation);
            await EvaluateAsync($"void egrooRuntime.start('{id}')", navigation);

            var timeout = DateTime.UtcNow.AddMinutes(2);
            var result = new StringBuilder();
            while (DateTime.UtcNow < timeout)
            {
                if (await EvaluateAsync($"egrooRuntime.read('{id}', {result.Length})", navigation) is { } part)
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
            if (navigation == _navigation)
            {
                try
                {
                    await EvaluateAsync($"globalThis.egrooRuntime?.clear('{id}')", navigation);
                }
                catch (Exception ex)
                {
                    // Cleanup must preserve the outcome of the original call.
                    Debug.WriteLine(ex);
                }
            }
        }
    }

    private async Task<TaskCompletionSource<bool>> WaitForRuntimeAsync()
    {
        if (_retryNavigation)
            ReloadRuntime();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            while (true)
            {
                var navigation = _navigation;
                if (await navigation.Task.WaitAsync(timeout.Token))
                {
                    // The native page can report navigation before its scripts finish loading.
                    var scriptWait = Stopwatch.StartNew();
                    while (navigation == _navigation && scriptWait.Elapsed < TimeSpan.FromSeconds(3))
                    {
                        var runtimeType = await EvaluateAsync("typeof globalThis.egrooRuntime", navigation).WaitAsync(timeout.Token);
                        if (runtimeType == "object" && navigation == _navigation)
                            return navigation;
                        await Task.Delay(100, timeout.Token);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
                }

                if (navigation == _navigation)
                    ReloadRuntime();
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            _retryNavigation = !_navigation.Task.IsCompleted;
            throw new TimeoutException(_unavailableMessage);
        }
    }

    private void ResetNavigation()
    {
        if (_navigation.Task.IsCompleted)
            _navigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void ReloadRuntime()
    {
        _retryNavigation = false;
        ResetNavigation();
        _view.Navigate(_runtimePage);
    }

    private async Task<string?> EvaluateAsync(string expression, TaskCompletionSource<bool> navigation)
    {
        if (navigation != _navigation)
            throw new InvalidOperationException("The Egroo runtime page changed. Try again.");
        var value = await _view.InvokeScript(expression);
        if (value?.StartsWith('"') == true)
            return JsonSerializer.Deserialize<string>(value);
        return value is "null" or "undefined" ? null : value;
    }
}
