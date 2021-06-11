using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace xamarinchatsr.RestClient
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
        //public Users users = new Users();

        public string GetResponse()
        {
            string res = ResponseTxt;
            //ResponseTxt = string.Empty;
            return res;
        }

        public Task<T> GetResponseModel()
        {
            T taskModels = JsonConvert.DeserializeObject<T>(GetResponse());
            return Task.FromResult(taskModels);
        }

        public async Task<T> GetAsync(string WebServiceUrl, string authKey = null, AuthType authType = AuthType.None)
        {
            try
            {
                HttpClient httpClient = new HttpClient();

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

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

                T taskModels = JsonConvert.DeserializeObject<T>(json);

                return taskModels;
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        public async Task<bool> PostAsync(T t, string WebServiceUrl, string authKey = null, AuthType authType = AuthType.None)
        {
            HttpClient httpClient = new HttpClient();
            byte[] authToken;

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

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

            string json = JsonConvert.SerializeObject(t);

            HttpContent httpContent = new StringContent(json);

            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage result = await httpClient.PostAsync(WebServiceUrl, httpContent);
            ResponseTxt = await result.Content.ReadAsStringAsync();

            return result.IsSuccessStatusCode;
        }

        public async Task<bool> PostHTTPSAsync(T t, string WebServiceUrl)
        {
            HttpClient httpClient = new HttpClient();

            //https://stackoverflow.com/questions/22251689/make-https-call-using-httpclient
            //specify to use TLS 1.2 as default connection
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            string json = JsonConvert.SerializeObject(t);

            HttpContent httpContent = new StringContent(json);

            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage result = await httpClient.PostAsync(WebServiceUrl, httpContent);

            return result.IsSuccessStatusCode;
        }

        public Task<bool> PostHTTPSAsyncRaw(string json, string WebServiceUrl)
        {
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(WebServiceUrl);

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string result = streamReader.ReadToEnd();
                }
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }
    }
}