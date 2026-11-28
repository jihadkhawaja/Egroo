namespace Egroo.UI.Services;

public sealed class StorageService(ClientRuntime runtime)
{
    public Task<string?> GetFromLocalStorage(string key) => GetFromLocalStorage(key, CancellationToken.None);
    internal Task<string?> GetFromLocalStorage(string key, CancellationToken cancellationToken) =>
        runtime.InvokeAsync<string?>("storage.get", cancellationToken, key);
    public Task SetLocalStorage(string key, string value) => runtime.InvokeVoidAsync("storage.set", key, value);
    public Task RemoveFromLocalStorage(string key) => runtime.InvokeVoidAsync("storage.remove", key);
}
