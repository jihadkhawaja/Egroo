using MobileChat.Server.Interfaces;
using MobileChat.Server.Rest;
using MobileChat.Shared.Models;
using System.Text.Json;

namespace MobileChat.Server.Services
{
    public class FirebaseNotficationService : IFirebaseNotification
    {
        private readonly RestClient RestClient;
        public FirebaseNotficationService()
        {
            RestClient = new RestClient();
        }
        public async Task<bool> Send(string token, string title, string message)
        {
            Firebase.Response response = JsonSerializer.Deserialize<Firebase.Response>(await RestClient.PostAsync(new Firebase.Message()
            {
                To = token,
                Data = new Firebase.Data() { Message = title, Body = message },
            }, "https://fcm.googleapis.com/fcm/send",
            "",
            AuthType.Token));

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
            Firebase.Response response = JsonSerializer.Deserialize<Firebase.Response>(await RestClient.PostAsync(new Firebase.Message()
            {
                To = "general",
                Data = new Firebase.Data() { Message = title, Body = message },
            }, "https://fcm.googleapis.com/fcm/send",
            "",
            AuthType.Token));

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
