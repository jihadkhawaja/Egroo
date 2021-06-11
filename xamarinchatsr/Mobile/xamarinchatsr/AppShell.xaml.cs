using xamarinchatsr.Views;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace xamarinchatsr
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));

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
        }
    }
}