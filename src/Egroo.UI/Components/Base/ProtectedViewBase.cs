using Egroo.UI.Services;
using Microsoft.AspNetCore.Components;

namespace Egroo.UI.Components.Base
{
    public class ProtectedViewBase : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = null!;
        [Inject]
        private SessionStorage SessionStorage { get; set; } = null!;

        public bool IsBusy { get; set; }

        protected override void OnInitialized()
        {
            if (string.IsNullOrEmpty(SessionStorage.Token)
                || SessionStorage.User is null)
            {
                NavigationManager.NavigateTo("/");
            }
            else
            {
                OnAccess();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            if (string.IsNullOrEmpty(SessionStorage.Token)
                || SessionStorage.User is null)
            {
                NavigationManager.NavigateTo("/");
            }
            else
            {
                await OnAccessAsync();
            }
        }

        protected virtual void OnAccess()
        {
        }

        protected virtual Task OnAccessAsync()
        {
            return Task.CompletedTask;
        }
    }
}
