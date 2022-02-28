using MobileChat.Helpers;
using MobileChat.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui;

namespace MobileChat.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WalkthroughPage : ContentPage
    {
        public WalkthroughPage()
        {
            InitializeComponent();

            App.CurrentPage = this.GetType().Name;

            var list = new List<WalkthroughItem>
            {
                new WalkthroughItem(
                    IconFont.Smile,
                    "w1"),
                new WalkthroughItem(
                    IconFont.Comments,
                    "w2"),
                new WalkthroughItem(
                    "",
                    "Xamarin Chat SignalR")
            };
            TheCarousel.ItemsSource = list;
        }

        private async void TheCarousel_PositionChanged(object sender, PositionChangedEventArgs e)
        {
            if (e.CurrentPosition == 2)
            {
                TheCarousel.IsSwipeEnabled = false;
                indicatorview.IsVisible = false;
                await Task.Delay(500);
                App.Current.MainPage = new AppShell();
            }
        }
    }
}