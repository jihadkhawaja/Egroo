using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MobileChat.Shared.Rest
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
    public class RestClient
    {
        private readonly HttpClient httpClient;
        public RestClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<T> GetAsync<T>(string WebServiceUrl, string authKey = null, AuthType authType = AuthType.None)
        {
            try
            {
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

                T taskModels = JsonSerializer.Deserialize<T>(json);

                return taskModels;
            }
            catch (Exception)
            {
                return default;
            }
        }

        public async Task<KeyValuePair<bool, string>> PostAsync<T>(T t, string WebServiceUrl, string authKey = null, AuthType authType = AuthType.None)
        {
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

            string resultText = await result.Content.ReadAsStringAsync();

            return new KeyValuePair<bool, string>(result.IsSuccessStatusCode, resultText);
        }
    }
}