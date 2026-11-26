using Egroo.Contracts.Models;

namespace Egroo.Contracts.Interfaces;

public interface IAccountRecovery
{
    Task<AccountRecovery.Status> GetRecoveryStatus();
    Task<Operation.Result> BeginRecoveryEmailEnrollment(string address, string password, string? verificationCode);
    Task<Operation.Result> ConfirmRecoveryEmail(string code);
    Task<Operation.Result> RemoveRecoveryEmail(string password, string? verificationCode);
    Task<AccountRecovery.ResetResult> ResetPassword(string username, string code, string newPassword, string? verificationCode);
}
