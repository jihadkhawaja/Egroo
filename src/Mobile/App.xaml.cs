using MobileChat.Cache;
using MobileChat.Models;
using MobileChat.ViewModel;
using MobileChat.Views;
using System.Globalization;

namespace MobileChat;

public partial class App : Application
{
    //set to false in production
    public static bool isDebug = true;

    //public static AppSettings appSettings;
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

    //SignalR Web URL example (http://localhost:2736/chathub) where the chat web app is hosted
    public const string hubConnectionURL = "";

    //follow me and give this repo a star if you liked it <3
    public const string feedback = "https://twitter.com/jihadkhawaja";
    public App()
    {
        //cultureInfo = CultureInfo.InstalledUICulture;

        //SavingManager.FileManager.CreateDirectory("appsettings", "data");

        //appSettings = new AppSettings();
        //try
        //{
        //    appSettings = SavingManager.JsonSerialization.ReadFromJsonFile<AppSettings>("appsettings/user");

        //    cultureInfo = new CultureInfo(appSettings.language);
        //    Thread.CurrentThread.CurrentUICulture = cultureInfo;
        //    //AppResources.Culture = cultureInfo;
        //    appSettings.language = cultureInfo.ToString();
        //}
        //catch { }

        InitializeComponent();
        //MainPage = new MainPage();
        MainPage = new SettingsPage();
        //chat = new ChatViewModel();

        //if (appSettings.PRELaunched)
        //{
        //    if (isDebug)
        //    {
        //        MainPage = new WalkthroughPage();
        //    }
        //    else
        //    {
        //        MainPage = new AppShell();
        //    }
        //}
        //else
        //{
        //    appSettings.PRELaunched = true;

        //    MainPage = new WalkthroughPage();

        //    SavingManager.JsonSerialization.WriteToJsonFile<AppSettings>("appsettings/user", appSettings);
        //}
    }

    //protected override async void OnStart()
    //{
    //    await chat.Connect();
    //}

    //protected override void OnSleep()
    //{
    //}

    //protected override void OnResume()
    //{
    //}
}
