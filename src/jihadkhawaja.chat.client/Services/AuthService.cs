using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using System.Net.Http.Json;

namespace jihadkhawaja.chat.client.Services
{
    public class AuthService : IAuthClient
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

        public async Task<Operation.Response> SignIn(string username, string password)
        {
            var req = new { Username = username, Password = password };
            var httpResponse = await HttpClient.PostAsJsonAsync("api/v1/Auth/signin", req);

            if (!httpResponse.IsSuccessStatusCode)
                return new Operation.Response { Success = false, Message = $"HTTP error: {httpResponse.StatusCode}" };

            var response = await httpResponse.Content.ReadFromJsonAsync<Operation.Response>();
            return response ?? new Operation.Response { Success = false, Message = "Empty response from API" };
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
