using xamarinchatsr.Helpers;
using xamarinchatsr.Models;
using xamarinchatsr.Resources;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace xamarinchatsr.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WalkthroughPage : ContentPage
    {
        public WalkthroughPage()
        {
            InitializeComponent();

            App.CurrentPage = this.GetType().Name;

            switch (App.appSettings.language)
            {
                case "en":
                    FlowDirection = FlowDirection.LeftToRight;
                    break;

                case "ar":
                    FlowDirection = FlowDirection.RightToLeft;
                    break;

                case "en-US":
                    FlowDirection = FlowDirection.LeftToRight;
                    break;

                case "ar-LB":
                    FlowDirection = FlowDirection.RightToLeft;
                    break;

                default:
                    FlowDirection = FlowDirection.LeftToRight;
                    break;
            }

            var list = new List<WalkthroughItem>
            {
                new WalkthroughItem(
                    IconFont.Smile,
                    AppResources.walkthrought1),
                new WalkthroughItem(
                    IconFont.Comments,
                    AppResources.walkthrought2),
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