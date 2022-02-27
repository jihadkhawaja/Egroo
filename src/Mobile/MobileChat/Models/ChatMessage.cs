using Plugin.DeviceInfo;
using System;
using System.Text.Json.Serialization;

namespace MobileChat.Models
{
    [Serializable]
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string DeviceId { get; set; } = CrossDeviceInfo.Current.Id;

        public string UserName { get; set; }

        public string Message { get; set; }

        public string UrlPhoto { get; set; }

        public string AttachImg { get; set; }

        public string AttachAudio { get; set; }

        public string SentState { get; set; }

        public long DateMessageTimeSpan { get; set; } = DateTime.Now.Ticks;

        public string ImageBackgroundColor { get; set; } = "#232b2b";

        public bool AllowFullscreen { get; set; } = true;

        [JsonIgnore]
        public bool IsYourMessage { get; set; }

        [JsonIgnore]
        public string DateMessageDate
        {
            get
            {
                var date = DateTime.FromBinary(DateMessageTimeSpan);
                return date.ToString("f");
            }
        }
    }
}