using Microsoft.JSInterop;

namespace Egroo.UI.Helpers
{
    public class SignalCallbackHelper
    {
        private readonly Func<string, Task> _callback;

        public SignalCallbackHelper(Func<string, Task> callback)
        {
            _callback = callback;
        }

        [JSInvokable]
        public async Task OnSignalGenerated(string json)
        {
            await _callback(json);
        }
    }

}
