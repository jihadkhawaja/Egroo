using Microsoft.JSInterop;

namespace Egroo.UI.Services
{
    public class LocalStorageService
    {
        private readonly IJSRuntime _js;

        public LocalStorageService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<string?> GetFromLocalStorage(string key)
        {
            try
            {
                return await _js.InvokeAsync<string>("localStorage.getItem", key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while getting item from local storage: {ex.Message}");
                return null;
            }
        }

        public async Task SetLocalStorage(string key, string value)
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while setting item in local storage: {ex.Message}");
            }
        }

        public async Task RemoveLocalStorage(string key)
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while removing item from local storage: {ex.Message}");
            }
        }
    }
}
