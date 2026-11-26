using System.Net.Http.Json;
using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;

namespace Egroo.Client.Services;

public sealed class AccountRecoveryService(HttpClient http) : IAccountRecovery
{
    private const string Route = "api/v1/Auth/recovery/";

    public async Task<bool> IsPasswordRecoveryAvailable()
    {
        using var response = await http.GetAsync(Route + "availability");
        return response.IsSuccessStatusCode && (await response.Content.ReadFromJsonAsync<AccountRecovery.Availability>())?.Available == true;
    }

    public async Task<AccountRecovery.Status> GetRecoveryStatus()
    {
        using var response = await http.GetAsync(Route + "email");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountRecovery.Status>()
            ?? throw new InvalidOperationException("The API did not return recovery status.");
    }

    public Task<Operation.Result> BeginRecoveryEmailEnrollment(string address, string password, string? verificationCode) =>
        PostResultAsync("email/enrollment", new { Address = address, Password = password, VerificationCode = verificationCode });

    public Task<Operation.Result> ConfirmRecoveryEmail(string code) =>
        PostResultAsync("email/confirm", new { Code = code });

    public Task<Operation.Result> RemoveRecoveryEmail(string password, string? verificationCode) =>
        PostResultAsync("email/remove", new { Password = password, VerificationCode = verificationCode });

    public Task<Operation.Result> RequestPasswordRecovery(string username, string address) =>
        PostResultAsync("request", new { Username = username, Address = address });

    public async Task<AccountRecovery.ResetResult> ResetPassword(string username, string code, string newPassword, string? verificationCode)
    {
        using var response = await http.PostAsJsonAsync(Route + "reset",
            new { Username = username, Code = code, NewPassword = newPassword, VerificationCode = verificationCode });
        if (!response.IsSuccessStatusCode)
            return new(false, $"HTTP error: {response.StatusCode}");
        return await response.Content.ReadFromJsonAsync<AccountRecovery.ResetResult>() ?? new(false, "Empty response from API.");
    }

    private async Task<Operation.Result> PostResultAsync(string path, object request)
    {
        using var response = await http.PostAsJsonAsync(Route + path, request);
        if (!response.IsSuccessStatusCode)
            return new() { Success = false, Message = $"HTTP error: {response.StatusCode}" };
        return await response.Content.ReadFromJsonAsync<Operation.Result>()
            ?? new() { Success = false, Message = "Empty response from API." };
    }
}
