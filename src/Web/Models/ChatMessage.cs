using Newtonsoft.Json;
using System;

namespace MobileChatWeb.Models
{
    [Serializable]
    public class ChatMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("deviceid")]
        public string DeviceId { get; set; }

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
        public long DateMessageTimeSpan { get; set; }

        [JsonProperty("imagebackgroundcolor")]
        public string ImageBackgroundColor { get; set; }

        [JsonProperty("allowfullscreen")]
        public bool AllowFullscreen { get; set; }

        [JsonIgnore]
        public bool IsYourMessage { get; set; }

        [JsonIgnore]
        public string DateMessageDate { get; set; }
    }
}