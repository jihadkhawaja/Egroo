using System.ComponentModel.DataAnnotations;

namespace Egroo.Server.Models;

public sealed class UserRecovery
{
    public Guid UserId { get; set; }
    public string? ProtectedAddress { get; set; }
    public string? ProtectedPendingAddress { get; set; }
    public string? ConfirmationHash { get; set; }
    public long? ConfirmationExpiresAt { get; set; }
    public long? ConfirmationSentAt { get; set; }
    public string? ResetHash { get; set; }
    public long? ResetExpiresAt { get; set; }
    public long? ResetSentAt { get; set; }

    [ConcurrencyCheck]
    public long Revision { get; set; }
}
