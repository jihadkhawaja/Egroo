using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Egroo.Avalonia.Android;

[Application]
public sealed class EgrooApplication(nint handle, JniHandleOwnership ownership)
    : AvaloniaAndroidApplication<App>(handle, ownership)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.ConfigureWebView = MainActivity.ConfigureWebView;
        return base.CustomizeAppBuilder(builder);
    }
}
