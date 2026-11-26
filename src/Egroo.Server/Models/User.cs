using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Egroo.Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Models
{
    [Index(nameof(Username), IsUnique = true)]
    public class User : UserDto
    {
        public string? Password { get; set; }
        [ConcurrencyCheck]
        [JsonIgnore]
        public long SessionRevision { get; set; }
        [JsonIgnore]
        public UserSecurity? UserSecuriy { get; set; }
    }

    public class UserSecurity
    {
        public Guid UserId { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public string? ProtectedTotpSecret { get; set; }
        public long? TotpEnrollmentExpiresAt { get; set; }
        public long? LastTotpStep { get; set; }
        public string? RecoveryCodeHashes { get; set; }
        public string? AllowedSignInRegions { get; set; }
        public int FailedFactorAttempts { get; set; }
        public long? FactorLockedUntil { get; set; }
        [ConcurrencyCheck]
        public long FactorRevision { get; set; }
    }
}
