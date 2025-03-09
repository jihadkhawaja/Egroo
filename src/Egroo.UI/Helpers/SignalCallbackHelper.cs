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

        [JSInvokable("Invoke")]
        public async Task Invoke(string message)
        {
            if (_callback != null)
            {
                await _callback(message);
            }
        }
    }
}
