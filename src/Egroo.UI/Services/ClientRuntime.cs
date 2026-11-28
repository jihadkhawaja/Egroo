using System.Text.Json;

namespace Egroo.UI.Services;

public abstract class ClientRuntime
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public event Func<string, string, string, Task>? MediaEvent;

    protected abstract Task<string> CallAsync(string request);

    protected virtual Task<string> CallAsync(string request, CancellationToken cancellationToken) =>
        CallAsync(request).WaitAsync(cancellationToken);

    public Task<T> InvokeAsync<T>(string method, params object?[] args) =>
        InvokeAsync<T>(method, CancellationToken.None, args);

    public async Task<T> InvokeAsync<T>(string method, CancellationToken cancellationToken, params object?[] args)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await CallAsync(JsonSerializer.Serialize(new { method, args }, JsonOptions), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var response = JsonDocument.Parse(result);
        if (response.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.GetString());

        return response.RootElement.GetProperty("value").Deserialize<T>(JsonOptions)!;
    }

    public Task InvokeVoidAsync(string method, params object?[] args) => InvokeAsync<JsonElement>(method, args);

    public Task InvokeVoidAsync(string method, CancellationToken cancellationToken, params object?[] args) =>
        InvokeAsync<JsonElement>(method, cancellationToken, args);

    public async Task DispatchMediaEventsAsync()
    {
        foreach (var item in await InvokeAsync<string[][]>("media.events"))
        {
            if (item.Length == 3 && MediaEvent is { } callback)
                await callback(item[0], item[1], item[2]);
        }
    }
}
