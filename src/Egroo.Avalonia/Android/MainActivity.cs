using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Webkit;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Egroo.Avalonia.Android;

[Activity(Label = "Egroo", Icon = "@drawable/egroo", Theme = "@style/Theme.AppCompat.DayNight.NoActionBar",
    MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private const int AudioRequestCode = 100;
    private PermissionRequest? _audioRequest;

    internal static void ConfigureWebView(NativeWebView webView, Uri workspace)
    {
        webView.AdapterCreated += (_, args) =>
        {
            if (args.TryGetPlatformHandle() is not IAndroidWebViewPlatformHandle handle
                || Java.Lang.Object.GetObject<WebView>(handle.WebKitWebView, JniHandleOwnership.DoNotTransfer) is not { } native)
                return;

            for (var context = native.Context; context is ContextWrapper wrapper; context = wrapper.BaseContext)
            {
                if (context is not MainActivity activity)
                    continue;

                native.Settings.MediaPlaybackRequiresUserGesture = false;
                native.SetWebChromeClient(new WorkspaceChromeClient(activity, workspace));
                break;
            }
        };
    }

    private void RequestMicrophone(PermissionRequest request)
    {
        if (CheckSelfPermission(Manifest.Permission.RecordAudio) == Permission.Granted)
        {
            request.Grant([PermissionRequest.ResourceAudioCapture]);
            return;
        }

        if (_audioRequest is not null)
        {
            request.Deny();
            return;
        }

        _audioRequest = request;
        RequestPermissions([Manifest.Permission.RecordAudio], AudioRequestCode);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != AudioRequestCode || _audioRequest is not { } request)
            return;

        _audioRequest = null;
        if (grantResults is [Permission.Granted])
            request.Grant([PermissionRequest.ResourceAudioCapture]);
        else
            request.Deny();
    }

    private sealed class WorkspaceChromeClient(MainActivity activity, Uri workspace) : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest? request)
        {
            if (request is null)
                return;

            if (Uri.TryCreate(request.Origin?.ToString(), UriKind.Absolute, out var origin)
                && Uri.Compare(workspace, origin, UriComponents.SchemeAndServer, UriFormat.UriEscaped,
                    StringComparison.OrdinalIgnoreCase) == 0
                && request.GetResources() is [PermissionRequest.ResourceAudioCapture])
                activity.RequestMicrophone(request);
            else
                request.Deny();
        }

        public override void OnPermissionRequestCanceled(PermissionRequest? request)
        {
            if (activity._audioRequest == request)
                activity._audioRequest = null;
        }

    }
}
