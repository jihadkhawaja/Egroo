using MobileChat.Cache;
using MobileChat.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.AspNetCore.SignalR.Client;

namespace MobileChat.ViewModel
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public int totalnewmessages = 0;
        private ChatMessage _chatmessage = new ChatMessage();
        private ChatInfo _chatinfo = new ChatInfo();
        private string _message;
        private string _totalusers;
        private ObservableCollection<ChatMessage> _messages;
        private bool _isConnected;
        private bool _isLoading;
        private string _displayname;

        public ChatMessage chatmessage
        {
            get
            {
                return _chatmessage;
            }
            set
            {
                _chatmessage = value;
                OnPropertyChanged();
            }
        }

        public ChatInfo chatinfo
        {
            get
            {
                return _chatinfo;
            }
            set
            {
                _chatinfo = value;
                OnPropertyChanged();
            }
        }

        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        public string UserName
        {
            get
            {
                return _displayname;
            }
            set
            {
                _displayname = value;
                OnPropertyChanged();
            }
        }

        public string TotalUsers
        {
            get
            {
                return _totalusers;
            }
            set
            {
                _totalusers = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ChatMessage> Messages
        {
            get
            {
                return _messages;
            }
            set
            {
                _messages = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            set
            {
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public bool AutoScrollDown;

        private HubConnection hubConnection;

        public Command SendMessageCommand { get; }
        public Command ConnectCommand { get; }
        public Command DisconnectCommand { get; }

        public ChatViewModel()
        {
            try
            {
                Messages = new ObservableCollection<ChatMessage>();
                SendMessageCommand = new Command(async () => { await SendMessage(chatmessage); });
                ConnectCommand = new Command(async () => await Connect());
                DisconnectCommand = new Command(async () => await Disconnect());

                chatinfo = new ChatInfo();

                IsConnected = false;

                hubConnection = new HubConnectionBuilder()
                 .WithUrl(App.hubConnectionURL)
                 .Build();

                hubConnection.On<ChatInfo>("JoinChat", chatinfo =>
                {
                    this.chatinfo = chatinfo;
                    TotalUsers = $"{chatinfo.totalUsers} users in chat";
                });

                hubConnection.On<ChatInfo>("LeaveChat", chatinfo =>
                {
                    this.chatinfo = chatinfo;
                    TotalUsers = $"{chatinfo.totalUsers} users in chat";
                });

                hubConnection.On<ChatMessage>("ReceiveMessage", chatmessage =>
                {
                    if (chatmessage.DeviceId == "ID123")
                        chatmessage.IsYourMessage = true;
                    else
                        chatmessage.IsYourMessage = false;

                    Messages.Add(chatmessage);

                    if (App.CurrentPage != "ChatPage")
                    {
                        totalnewmessages++;
                        MessagingCenter.Send(this, "ChangeTextBadge", totalnewmessages.ToString());
                    }

                    if (AutoScrollDown)
                        MessagingCenter.Send<ChatViewModel>(this, "ScrollToEnd");
                });

                hubConnection.On<List<ChatMessage>>("ReceiveOldMessage", chatmessages =>
                {
                    Messages.Clear();
                    foreach (ChatMessage cm in chatmessages)
                    {
                        if (cm.DeviceId == "ID123")
                            cm.IsYourMessage = true;
                        else
                            cm.IsYourMessage = false;

                        Messages.Add(cm);
                    }

                    MessagingCenter.Send<ChatViewModel>(this, "ScrollToEnd");
                });
            }
            catch { }
        }

        public async Task Connect()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                if (!IsConnected)
                {
                    await hubConnection.StartAsync();
                    await hubConnection.InvokeAsync("JoinChat");

                    IsConnected = true;
                }
            }
            catch (Exception e)
            {
                IsConnected = false;
                await App.Current.MainPage.DisplayAlert("Error", "Connection lost, Connect to the internet and try again", "Ok");
            }

            IsLoading = false;
        }

        private async Task SendMessage(ChatMessage chatmessage)
        {
            IsLoading = true;
            try
            {
                if (!string.IsNullOrEmpty(Message) && !string.IsNullOrWhiteSpace(Message))
                {
                    chatmessage.Message = Message;
                    await hubConnection.InvokeAsync("SendMessage", chatmessage);
                    Message = string.Empty;
                }

                MessagingCenter.Send<ChatViewModel>(this, "ScrollToEnd");
            }
            catch
            {
                await Connect();
            }
            IsLoading = false;
        }

        public async Task Disconnect()
        {
            await hubConnection.InvokeAsync("LeaveChat");
            await hubConnection.StopAsync();

            IsConnected = false;
        }

        public async Task CreateUsername(bool overwrite = false)
        {

            string results = await App.Current.MainPage.DisplayPromptAsync("what should we call u", "enter your display name", "Apply", "Close",
                "Anonymouse", 50, Keyboard.Chat, "");
        }

        public void ClearBadges()
        {
            totalnewmessages = 0;
            MessagingCenter.Send(this, "RemoveBadge");
        }
    }
}