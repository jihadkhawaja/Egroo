using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;

namespace Egroo.Client.Services
{
    [ExcludeFromCodeCoverage]
    public class AuthService : IAuthClient, ITwoFactorAuth
    {
        public HttpClient HttpClient { get; }

        public AuthService(HttpClient http)
        {
            HttpClient = http;
        }

        public async Task<Operation.Response> SignUp(string username, string password)
        {
            var req = new { Username = username, Password = password };
            var httpResponse = await HttpClient.PostAsJsonAsync("api/v1/Auth/signup", req);

            if (!httpResponse.IsSuccessStatusCode)
                return new Operation.Response { Success = false, Message = $"HTTP error: {httpResponse.StatusCode}" };

            var response = await httpResponse.Content.ReadFromJsonAsync<Operation.Response>();
            return response ?? new Operation.Response { Success = false, Message = "Empty response from API" };
        }

        public Task<Operation.Response> SignIn(string username, string password) =>
            SignIn(username, password, verificationCode: null);

        public async Task<Operation.Response> SignIn(string username, string password, string? verificationCode)
        {
            var req = new { Username = username, Password = password, VerificationCode = verificationCode };
            var httpResponse = await HttpClient.PostAsJsonAsync("api/v1/Auth/signin", req);

            if (!httpResponse.IsSuccessStatusCode)
                return new Operation.Response { Success = false, Message = $"HTTP error: {httpResponse.StatusCode}" };

            var response = await httpResponse.Content.ReadFromJsonAsync<Operation.Response>();
            return response ?? new Operation.Response { Success = false, Message = "Empty response from API" };
        }

        public async Task<TwoFactor.Status> GetTwoFactorStatus()
        {
            using var response = await HttpClient.GetAsync("api/v1/Auth/twofactor");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TwoFactor.Status>()
                ?? throw new InvalidOperationException("The API did not return two-factor status.");
        }

        public Task<TwoFactor.Enrollment> BeginTwoFactorEnrollment(string password) =>
            PostTwoFactorAsync("enrollment", new { Password = password },
                message => new TwoFactor.Enrollment(false, message));

        public Task<TwoFactor.Activation> EnableTwoFactor(string password, string verificationCode) =>
            PostTwoFactorAsync("enable", new { Password = password, VerificationCode = verificationCode },
                message => new TwoFactor.Activation(false, message));

        public Task<Operation.Result> DisableTwoFactor(string password, string verificationCode) =>
            PostTwoFactorAsync("disable", new { Password = password, VerificationCode = verificationCode },
                message => new Operation.Result { Success = false, Message = message });

        private async Task<T> PostTwoFactorAsync<T>(string path, object request, Func<string, T> failure) where T : class
        {
            using var response = await HttpClient.PostAsJsonAsync("api/v1/Auth/twofactor/" + path, request);
            return !response.IsSuccessStatusCode
                ? failure($"HTTP error: {response.StatusCode}")
                : await response.Content.ReadFromJsonAsync<T>() ?? failure("Empty response from API.");
        }

        public async Task<Operation.Response> RefreshSession()
        {
            var httpResponse = await HttpClient.GetAsync("api/v1/Auth/refreshsession");

            if (!httpResponse.IsSuccessStatusCode)
                return new Operation.Response { Success = false, Message = $"HTTP error: {httpResponse.StatusCode}" };

            var response = await httpResponse.Content.ReadFromJsonAsync<Operation.Response>();
            return response ?? new Operation.Response { Success = false, Message = "Empty response from API" };
        }

        public async Task<Operation.Result> ChangePassword(string oldpassword, string newpassword)
        {
            var req = new { OldPassword = oldpassword, NewPassword = newpassword };
            var httpResponse = await HttpClient.PutAsJsonAsync("api/v1/Auth/changepassword", req);

            if (!httpResponse.IsSuccessStatusCode)
                return new Operation.Result { Success = false, Message = $"HTTP error: {httpResponse.StatusCode}" };

            var response = await httpResponse.Content.ReadFromJsonAsync<Operation.Result>();
            return response ?? new Operation.Result { Success = false, Message = "Empty response from API" };
        }
    }
}
