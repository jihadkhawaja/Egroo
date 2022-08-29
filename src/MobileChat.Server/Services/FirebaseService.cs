using MobileChat.Server.Interfaces;
using MobileChat.Shared.Models;
using MobileChat.Shared.Rest;
using System.Text.Json;

namespace MobileChat.Server.Services
{
    public class FirebaseService : IFirebase
    {
        private readonly RestClient RestClient;
        public FirebaseService(RestClient RestClient)
        {
            this.RestClient = RestClient;
        }
        public async Task<bool> Send(string token, string title, string message)
        {
            KeyValuePair<bool, string> messageResponse = await RestClient.PostAsync(new Firebase.Message()
            {
                To = token,
                Data = new Firebase.Data() { Message = title, Body = message },
            }, "https://fcm.googleapis.com/fcm/send",
            "",
            AuthType.Token);

            if (!messageResponse.Key)
            {
                return false;
            }

            Firebase.Response response = JsonSerializer.Deserialize<Firebase.Response>(messageResponse.Value);

            if (response.Success == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> SendAll(string title, string message)
        {
            KeyValuePair<bool, string> messageResponse = await RestClient.PostAsync(new Firebase.Message()
            {
                To = "general",
                Data = new Firebase.Data() { Message = title, Body = message },
            }, "https://fcm.googleapis.com/fcm/send",
            "",
            AuthType.Token);

            if (!messageResponse.Key)
            {
                return false;
            }

            Firebase.Response response = JsonSerializer.Deserialize<Firebase.Response>(messageResponse.Value);

            if (response.Success == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
