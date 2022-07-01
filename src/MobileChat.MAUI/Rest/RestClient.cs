using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MobileChat.MAUI.Rest
{
    public enum AuthType
    {
        Basic,
        Token,
        None
    }

    /// <summary>
    /// RestClient implements methods for calling CRUD operations
    /// using HTTP.
    /// </summary>
    public class RestClient<T>
    {
        private string ResponseTxt = string.Empty;

        public string GetResponse()
        {
            return ResponseTxt;
        }

        public Task<T> GetResponseModel()
        {
            T taskModels = JsonSerializer.Deserialize<T>(GetResponse());
            return Task.FromResult(taskModels);
        }

        public async Task<T> GetAsync(string WebServiceUrl, string authKey = null, AuthType authType = AuthType.None)
        {
            try
            {
                HttpClient httpClient = new();

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                byte[] authToken;
                switch (authType)
                {
                    case AuthType.None:
                        break;

                    case AuthType.Basic:
                        authToken = Encoding.ASCII.GetBytes($"{authKey}:");
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(authToken));
                        break;

                    case AuthType.Token:
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                            authKey);
                        break;

                    default:
                        break;
                }

                string json = await httpClient.GetStringAsync(WebServiceUrl);

                ResponseTxt = json;

                T taskModels = JsonSerializer.Deserialize<T>(json);

                return taskModels;
            }
            catch (Exception)
            {
                return default;
            }
        }

        public async Task<bool> PostAsync(T t, string WebServiceUrl, string authKey = null, AuthType authType = AuthType.None)
        {
            HttpClient httpClient = new();
            byte[] authToken;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            switch (authType)
            {
                case AuthType.None:
                    break;

                case AuthType.Basic:
                    authToken = Encoding.ASCII.GetBytes($"{authKey}:");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(authToken));
                    break;

                case AuthType.Token:
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                        authKey);
                    break;

                default:
                    break;
            }

            string json = JsonSerializer.Serialize(t);

            HttpContent httpContent = new StringContent(json);

            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage result = await httpClient.PostAsync(WebServiceUrl, httpContent);
            ResponseTxt = await result.Content.ReadAsStringAsync();

            return result.IsSuccessStatusCode;
        }
    }
}