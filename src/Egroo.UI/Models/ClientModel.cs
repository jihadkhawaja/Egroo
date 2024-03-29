using Microsoft.AspNetCore.Components;

namespace Egroo.UI.Models
{
    public enum FrameworkPlatform
    {
        WASM = 0,
        SERVER,
        MAUI
    }
    public static class ClientModel
    {
        public static RenderFragment? MyMudThemeProvider { get; set; }
        public static RenderFragment? MyMudProvider { get; set; }
    }
}
