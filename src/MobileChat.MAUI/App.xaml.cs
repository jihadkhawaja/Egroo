using Microsoft.AspNetCore.Components;
using MobileChat.Client;
using MobileChat.MAUI.Interfaces;
using MobileChat.MAUI.Models;
using MobileChat.MAUI.Services;
using MobileChat.Shared.Models;

namespace MobileChat.MAUI
{
    public partial class App : Application
    {
        // SignalR chat hub name (http://your-web-url/hubName)
        public const string hubName = "chathub";
#if DEBUG
        // Development
        // SignalR Web URL example (http://localhost:2736/) where the chat web app is hosted
        public const string hubConnectionURL = "https://localhost:7175/" + hubName;
#else
        //production
        //SignalR Web URL example (https://www.domain.com/) where the chat web app is hosted
        public const string hubConnectionURL = "your address here" + hubName;
#endif

        public CancellationTokenSource ConnectionCancellationTokenSource { get; private set; }
        public App()
        {
            // Initialize client chat signalr service with your server chat hub url
            ClientChat.Initialize(hubConnectionURL);

            InitializeComponent();

            MainPage = new MainPage();
        }

        protected override async void OnStart()
        {
            //connect to the server through SignalR chathub
            ConnectionCancellationTokenSource = new();
            if (await ClientChat.SignalR.Connect(ConnectionCancellationTokenSource))
            {
                Console.WriteLine("Connected!");
            }
        }
    }
}