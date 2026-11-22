using System.Text.Json;

namespace Egroo.UI.Services;

public abstract class ClientRuntime
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public event Func<string, string, string, Task>? MediaEvent;

    protected abstract Task<string> CallAsync(string request);

    public async Task<T> InvokeAsync<T>(string method, params object?[] args)
    {
        using var response = JsonDocument.Parse(await CallAsync(JsonSerializer.Serialize(new { method, args }, JsonOptions)));
        if (response.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.GetString());

        return response.RootElement.GetProperty("value").Deserialize<T>(JsonOptions)!;
    }

    public Task InvokeVoidAsync(string method, params object?[] args) => InvokeAsync<JsonElement>(method, args);

    public async Task DispatchMediaEventsAsync()
    {
        foreach (var item in await InvokeAsync<string[][]>("media.events"))
        {
            if (item.Length == 3 && MediaEvent is { } callback)
                await callback(item[0], item[1], item[2]);
        }
    }
}
