namespace jihadkhawaja.chat.shared.Models
{
    public class Operation
    {
        public class Response
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public Guid? UserId { get; set; }
            public string? Token { get; set; }
        }

        public class Result
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }
    }
}
