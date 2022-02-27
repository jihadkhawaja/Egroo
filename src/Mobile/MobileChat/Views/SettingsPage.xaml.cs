using MobileChat.Cache;
using MobileChat.Interface;
using MobileChat.Models;
using MobileChat.Themes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileChat.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPage : ContentPage, INotifyPropertyChanged
    {
        public SettingsPage()
        {
            BindingContext = this;
            InitializeComponent();

            switch (App.appSettings.theme)
            {
                case AppSettings.Theme.Dark:
                    themeswitch.IsToggled = true;
                    break;

                case AppSettings.Theme.Light:
                    themeswitch.IsToggled = false;
                    break;

                default:
                    themeswitch.IsToggled = true;
                    break;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            App.CurrentPage = this.GetType().Name;

            RefreshTheme();
        }

        private void RefreshTheme()
        {
            //set theme
            ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            if (mergedDictionaries != null)
            {
                mergedDictionaries.Clear();
                switch (App.appSettings.theme)
                {
                    case AppSettings.Theme.Dark:
                        mergedDictionaries.Add(new Dark());
                        break;

                    case AppSettings.Theme.Light:
                        mergedDictionaries.Add(new Light());
                        break;

                    default:
                        mergedDictionaries.Add(new Dark());
                        break;
                }
            }
        }

        /// <summary>
        /// twitter tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            try
            {
                if (await Launcher.TryOpenAsync(new Uri(App.feedback)))
                {
                    //success
                }
                else
                {
                    await Browser.OpenAsync(new Uri(App.feedback), BrowserLaunchMode.SystemPreferred);
                }
            }
            catch { }
        }

        /// <summary>
        /// dark mode toggled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Switch_Toggled(object sender, ToggledEventArgs e)
        {
            ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            if (mergedDictionaries != null)
            {
                if (e.Value && App.appSettings.theme == AppSettings.Theme.Light)
                {
                    mergedDictionaries.Clear();

                    mergedDictionaries.Add(new Dark());

                    App.appSettings.theme = AppSettings.Theme.Dark;

                    SavingManager.JsonSerialization.WriteToJsonFile<AppSettings>("appsettings/user", App.appSettings);
                }
                else if (!e.Value && App.appSettings.theme == AppSettings.Theme.Dark)
                {
                    mergedDictionaries.Clear();

                    mergedDictionaries.Add(new Light());

                    App.appSettings.theme = AppSettings.Theme.Light;

                    SavingManager.JsonSerialization.WriteToJsonFile<AppSettings>("appsettings/user", App.appSettings);
                }
            }
        }

        /// <summary>
        /// Share App
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TapGestureRecognizer_Tapped_3(object sender, EventArgs e)
        {
            string content = "Chat with friends now!";

            string shareTitle = "Available on Apple App Store and Google Play Store";
            string links = string.Format("Download on iOS {0}\n Download on Android {1}", App.appStoreAppBaseURL + App.iOSAppID, App.playStoreAppBaseURL + App.playStoreAppID);

            await Share.RequestAsync(new ShareTextRequest
            {
                Text = $"{App.AppName}\n{shareTitle}\n{content}\n{links}",
                Title = App.AppName,
            });
        }
    }
}