using MobileChat.Cache;
using MobileChat.Interface;
using MobileChat.Models;
using MobileChat.Themes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui;

namespace MobileChat.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPage : ContentPage, INotifyPropertyChanged
    {
        public SettingsPage()
        {
            BindingContext = this;
            InitializeComponent();
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

        }

        /// <summary>
        /// Language tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TapGestureRecognizer_Tapped_1(object sender, EventArgs e)
        {
            string language = await DisplayActionSheet("Language", null, "Cancel", "English", "العربية");

            if (language == "Cancel")
                return;

            CultureInfo ci = new CultureInfo("en");

            DependencyService.Get<IToast>().Show("Please Restart The App To Apply Changes");
        }

        /// <summary>
        /// Rate App
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TapGestureRecognizer_Tapped_2(object sender, EventArgs e)
        {
            
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