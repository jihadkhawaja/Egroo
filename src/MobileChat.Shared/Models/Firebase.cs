using System.Collections.Generic;

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
            public long multicast_id { get; set; }
            public int success { get; set; }
            public int failure { get; set; }
            public int canonical_ids { get; set; }
            public List<Result> Results { get; set; }
        }
    }
}
