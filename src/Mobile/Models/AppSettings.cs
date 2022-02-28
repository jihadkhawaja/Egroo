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

        public bool PRELaunched { get; set; }
        public Theme theme { get; set; }
        public string language { get; set; } = "en";
        public string chatUserName { get; set; }
        public bool hasReviewdApp { get; set; }
    }
}