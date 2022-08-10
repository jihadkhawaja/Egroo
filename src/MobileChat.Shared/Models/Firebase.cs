namespace MobileChat.Shared.Models
{
    public class Firebase
    {
        public class Data
        {
            public string Message { get; set; }
            public string Body { get; set; }
        }

        public class Message
        {
            public string To { get; set; }
            public Data Data { get; set; }
        }

        public class Result
        {
            public string Message_id { get; set; }
        }

        public class Response
        {
            public long Multicast_id { get; set; }
            public int Success { get; set; }
            public int Failure { get; set; }
            public int Canonical_ids { get; set; }
            public List<Result> Results { get; set; }
        }
    }
}
