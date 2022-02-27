using MobileChat.Views;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MobileChat
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));

            FlowDirection = FlowDirection.LeftToRight;
        }
    }
}