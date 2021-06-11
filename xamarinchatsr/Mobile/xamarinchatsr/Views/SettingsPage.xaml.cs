using xamarinchatsr.Cache;
using xamarinchatsr.Interface;
using xamarinchatsr.Models;
using xamarinchatsr.Themes;
using Plugin.StoreReview;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace xamarinchatsr.Views
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

            if (CrossStoreReview.IsSupported)
            {
                if (!App.appSettings.hasReviewdApp)
                {
                    CrossStoreReview.Current.RequestReview(false);
                    App.appSettings.hasReviewdApp = true;

                    SavingManager.JsonSerialization.WriteToJsonFile<AppSettings>("appsettings/user", App.appSettings);
                }
            }
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
            if (await Launcher.TryOpenAsync(new Uri("https://twitter.com/jihadkhawaja")))
            {
                //success
            }
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
            switch (language)
            {
                case "English":
                    ci = new CultureInfo("en");
                    App.appSettings.language = ci.ToString();
                    break;

                case "العربية":
                    ci = new CultureInfo("ar");
                    App.appSettings.language = ci.ToString();
                    break;

                default:
                    ci = CultureInfo.InstalledUICulture;
                    App.appSettings.language = ci.ToString();
                    break;
            }

            SavingManager.JsonSerialization.WriteToJsonFile<AppSettings>("appsettings/user", App.appSettings);

            DependencyService.Get<IToast>().Show("Please Restart The App To Apply Changes");
        }

        /// <summary>
        /// Rate App
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TapGestureRecognizer_Tapped_2(object sender, EventArgs e)
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                if (CrossStoreReview.IsSupported)
                {
                    CrossStoreReview.Current.OpenStoreReviewPage(App.iOSAppID);
                }
                else
                {
                    if (await Launcher.TryOpenAsync(new Uri($"{App.appStoreAppBaseURL}{App.iOSAppID}")))
                    {
                        //success
                    }
                }
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                if (CrossStoreReview.IsSupported)
                {
                    CrossStoreReview.Current.OpenStoreReviewPage(App.playStoreAppID);
                }
                else
                {
                    if (await Launcher.TryOpenAsync(new Uri($"{App.playStoreAppBaseURL}{App.playStoreAppID}")))
                    {
                        //success
                    }
                }
            }
            else if (Device.RuntimePlatform == Device.UWP)
            {
                if (CrossStoreReview.IsSupported)
                {
                    CrossStoreReview.Current.OpenStoreReviewPage("");
                }
                else
                {
                    if (await Launcher.TryOpenAsync(new Uri($"")))
                    {
                        //success
                    }
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
            string content = "Economy, Fuel & Gold day by day data to fulfill the Lebanese needs.";

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