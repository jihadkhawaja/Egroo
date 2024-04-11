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

        protected override async Task OnInitializedAsync()
        {
            if (string.IsNullOrEmpty(SessionStorage.Token)
                || SessionStorage.User is null)
            {
                NavigationManager.NavigateTo("/");
            }
            else
            {
                await OnAccess();
            }
        }

        protected virtual Task OnAccess()
        {
            return Task.CompletedTask;
        }
    }
}
