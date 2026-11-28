using Egroo.Contracts.Models;

namespace Egroo.Contracts.Interfaces;

public interface ITwoFactorAuth
{
    Task<Operation.Response> SignIn(string username, string password, string? verificationCode);
    Task<TwoFactor.Status> GetTwoFactorStatus();
    Task<TwoFactor.Enrollment> BeginTwoFactorEnrollment(string password);
    Task<TwoFactor.Activation> EnableTwoFactor(string password, string verificationCode);
    Task<Operation.Result> DisableTwoFactor(string password, string verificationCode);
}
