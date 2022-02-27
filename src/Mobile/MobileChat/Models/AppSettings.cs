using System;

namespace MobileChat.Models
{
    [Serializable]
    public class AppSettings
    {
        public enum Theme
        {
            Dark,
            Light
        }

        public Theme theme { get; set; }
        public string chatUserName { get; set; }
    }
}