using Egroo.UI.Core;

namespace Egroo.MAUI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new MainPage();
        }

        protected override async void OnStart()
        {
            await HubInitializer.Connect();
        }
    }
}