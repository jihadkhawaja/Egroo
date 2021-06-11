using Newtonsoft.Json;
using Plugin.DeviceInfo;
using System;

namespace xamarinchatsr.Models
{
    [Serializable]
    public class ChatMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("deviceid")]
        public string DeviceId { get; set; } = CrossDeviceInfo.Current.Id;

        [JsonProperty("username")]
        public string UserName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("photo")]
        public string UrlPhoto { get; set; }

        [JsonProperty("attach")]
        public string AttachImg { get; set; }

        [JsonProperty("audio")]
        public string AttachAudio { get; set; }

        [JsonProperty("sentstate")]
        public string SentState { get; set; }

        [JsonProperty("datemessage")]
        public long DateMessageTimeSpan { get; set; } = DateTime.Now.Ticks;

        [JsonProperty("imagebackgroundcolor")]
        public string ImageBackgroundColor { get; set; } = "#232b2b";

        [JsonProperty("allowfullscreen")]
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