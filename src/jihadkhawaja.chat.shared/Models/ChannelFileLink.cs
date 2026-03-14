namespace jihadkhawaja.chat.shared.Models
{
    public class ChannelFileLink
    {
        public Guid ChannelId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long SizeBytes { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}