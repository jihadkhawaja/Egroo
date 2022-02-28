using MobileChat.Helpers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui;

namespace MobileChat.ViewModel
{
    public class ShellViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private string badgeTextChat;

        public string BadgeTextChat
        {
            get { return badgeTextChat; }
            set
            {
                badgeTextChat = value;
                OnPropertyChanged();
            }
        }

        private Color badgeColorChat;

        public Color BadgeColorChat
        {
            get { return badgeColorChat; }
            set
            {
                badgeColorChat = value;
                OnPropertyChanged();
            }
        }

        public ShellViewModel()
        {
            BadgeTextChat = "";
            MessagingCenter.Subscribe<ChatViewModel>(this, "RemoveBadge", app => RemoveBadge());
            MessagingCenter.Subscribe<ChatViewModel, string>(this, "ChangeColorBadge", (sender, value) => ChangeColor(value));
            MessagingCenter.Subscribe<ChatViewModel, string>(this, "ChangeTextBadge", (sender, value) => ChangeText(value));
        }

        /// <summary>
        /// The ChangeColor.
        /// </summary>
        private void ChangeColor(string text)
        {
            if (string.IsNullOrEmpty(BadgeTextChat))
                ChangeText(text);

            BadgeColorChat = (Color)ResourceHelper.GetResourceValue("PageBackgroundColorPrimary");
        }

        /// <summary>
        /// The ChangeText.
        /// </summary>
        private void ChangeText(string text)
        {
            BadgeTextChat = text;
        }

        /// <summary>
        /// The RemoveBadge.
        /// </summary>
        private void RemoveBadge()
        {
            BadgeTextChat = "";
        }
    }
}