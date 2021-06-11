using xamarinchatsr.Cache;
using xamarinchatsr.Models;
using xamarinchatsr.Resources;
using xamarinchatsr.ViewModel;
using xamarinchatsr.Views;
using System.Globalization;
using System.Threading;
using Xamarin.Forms;

namespace xamarinchatsr
{
    public partial class App : Application
    {
        public static bool isDebug = false;
        public static bool adsEnabled = true;

        public static bool viewdAD = false;

        public static AppSettings appSettings;
        public static CultureInfo cultureInfo;

        //chat
        public static ChatViewModel chat;

        public static string CurrentPage = "ChatPage";

        //const
        public const string iOSAppID = "";
        public const string appStoreAppBaseURL = "https://apps.apple.com/us/app/id";
        public const string playStoreAppID = "";
        public const string playStoreAppBaseURL = "https://play.google.com/store/apps/details?id=";
        public const string AppName = "Xamarin Chat SR";
        public const string hubConnectionURL = "";

        public App()
        {
            cultureInfo = CultureInfo.InstalledUICulture;

            SavingManager.FileManager.CreateDirectory("appsettings", "data");

            appSettings = new AppSettings();
            try
            {
                appSettings = SavingManager.JsonSerialization.ReadFromJsonFile<AppSettings>("appsettings/user");

                cultureInfo = new CultureInfo(appSettings.language);
                Thread.CurrentThread.CurrentUICulture = cultureInfo;
                AppResources.Culture = cultureInfo;
                appSettings.language = cultureInfo.ToString();
            }
            catch { }

            InitializeComponent();

            chat = new ChatViewModel();

            if (appSettings.PRELaunched)
            {
                if (isDebug)
                {
                    MainPage = new WalkthroughPage();
                }
                else
                {
                    MainPage = new AppShell();
                }
            }
            else
            {
                appSettings.PRELaunched = true;

                MainPage = new WalkthroughPage();

                SavingManager.JsonSerialization.WriteToJsonFile<AppSettings>("appsettings/user", appSettings);
            }
        }

        protected override async void OnStart()
        {
            await chat.Connect();
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}