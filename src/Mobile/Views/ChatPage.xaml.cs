using MobileChat.Models;
using MobileChat.Themes;
using MobileChat.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui;

namespace MobileChat.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChatPage : ContentPage
    {
        public ChatPage()
        {
            this.BindingContext = App.chat;
            InitializeComponent();

            Subscribe();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            App.CurrentPage = this.GetType().Name;

            //set theme
            ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            if (mergedDictionaries != null)
            {
                mergedDictionaries.Clear();
            }

            if (string.IsNullOrEmpty(App.chat.chatmessage.UserName))
                await App.chat.CreateUsername();

            await App.chat.Connect();

            ScrollToEnd(false);

            App.chat.ClearBadges();
        }

        private void Subscribe()
        {
            MessagingCenter.Subscribe<ChatViewModel>(this, "ScrollToEnd", (sender) =>
            {
                ScrollToEnd();
            });
        }

        private void ScrollToEnd(bool animated = true)
        {
            //var v = ChatList.ItemsSource.Cast<object>().LastOrDefault();
            //ChatList.ScrollTo(v, ScrollToPosition.End, animated);
        }

        private void TapGestureRecognizer_Tapped_2(object sender, EventArgs e)
        {
            var layout = (BindableObject)sender;
            var item = (ChatMessage)layout.BindingContext;

            if (item.AllowFullscreen)
            {
                //imageShowcase.Source = item.AttachImg;
                //imageShowcaseHolder.IsVisible = true;
            }
        }

        private Image playImage;
        private bool finishedPlay = true;

        private async void TapGestureRecognizer_Tapped_1(object sender, EventArgs e)
        {
            
        }

        private Task PlayAudio(string audioPath)
        {

            return Task.CompletedTask;
        }

        private void Player_FinishedPlaying(object sender, EventArgs e)
        {
            playImage.Source = "audio_play.png";
            finishedPlay = true;
        }

        /// <summary>
        /// close fullscreen image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Clicked(object sender, EventArgs e)
        {
            //imageShowcaseHolder.IsVisible = false;
            //imageShowcase.Source = "";
        }

        private void ChatList_ItemAppearing(object sender, ItemVisibilityEventArgs e)
        {
            //if ((ChatMessage)e.Item == App.chat.Messages[0])
            //{
            //    //First Item has been hit
            //}

            if ((ChatMessage)e.Item == App.chat.Messages[App.chat.Messages.Count - 1])
            {
                //Last Item has been hit
                App.chat.AutoScrollDown = true;
            }
            else
            {
                App.chat.AutoScrollDown = false;
            }
        }

        /// <summary>
        /// change User Name
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            await App.chat.CreateUsername(true);
        }
    }
}