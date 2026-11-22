namespace Egroo.UI.Services;

public sealed class StorageService(ClientRuntime runtime)
{
    public Task<string?> GetFromLocalStorage(string key) => runtime.InvokeAsync<string?>("storage.get", key);
    public Task SetLocalStorage(string key, string value) => runtime.InvokeVoidAsync("storage.set", key, value);
    public Task RemoveFromLocalStorage(string key) => runtime.InvokeVoidAsync("storage.remove", key);
}
