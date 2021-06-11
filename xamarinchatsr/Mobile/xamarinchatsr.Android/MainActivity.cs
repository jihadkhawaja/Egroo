using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using FFImageLoading.Forms.Platform;
using Plugin.CurrentActivity;

namespace xamarinchatsr.Droid
{
    [Activity(Label = "Xamarin Chat SR", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true, NoHistory = false,
        ScreenOrientation = ScreenOrientation.Portrait, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);

            Xamarin.Forms.Forms.SetFlags("IndicatorView_Experimental");

            global::Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            CarouselView.FormsPlugin.Droid.CarouselViewRenderer.Init();
            CachedImageRenderer.Init(true);
            CrossCurrentActivity.Current.Init(this, savedInstanceState);
            Xam.Shell.Badge.Droid.BottomBar.Init();
            Plugin.InputKit.Platforms.Droid.Config.Init(this, savedInstanceState);

            LoadApplication(new App());
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}