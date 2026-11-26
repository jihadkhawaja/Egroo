using System.Text.Json.Serialization;

namespace Egroo.Contracts.Models
{
    public class Operation
    {
        public class Response
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public Guid? UserId { get; set; }
            public string? Token { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public bool RequiresTwoFactor { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public bool RequiresRecoveryCode { get; set; }
        }

        public class Result
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }
    }
}
