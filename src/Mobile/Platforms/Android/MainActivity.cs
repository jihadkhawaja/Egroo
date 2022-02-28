using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MobileChat;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		Platform.Init(this, savedInstanceState);
		//CarouselView.FormsPlugin.Droid.CarouselViewRenderer.Init();
		//CachedImageRenderer.Init(true);
		//CrossCurrentActivity.Current.Init(this, savedInstanceState);
		//Xam.Shell.Badge.Droid.BottomBar.Init();
		//Plugin.InputKit.Platforms.Droid.Config.Init(this, savedInstanceState);
	}

	public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
	{
		Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

		base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
	}
}
