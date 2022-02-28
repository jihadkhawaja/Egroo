using MobileChat.Views;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MobileChat;

namespace MobileChat
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            //Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            //Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
        }
    }
}