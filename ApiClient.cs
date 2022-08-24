using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ApiClient
{
    public abstract class ApiClient<TControllersEnum> where TControllersEnum : Enum
    {
        private const string SerializeKey = "ApiCredentials";

        [JsonIgnore]
        public abstract string ApiAddress { get; set; }

        [JsonProperty("AccessToken")]
        private string _accessToken { get; set; }

        [JsonProperty("RefreshToken")]
        private string _refreshToken { get; set; }


        [JsonIgnore]
        public bool HasAccessToken => !String.IsNullOrEmpty(_accessToken);
        [JsonIgnore]
        public bool HasRefreshToken => !String.IsNullOrEmpty(_refreshToken);

        private protected IDataStore _dataStore;

        protected ApiClient(IDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public virtual async Task<bool> RestoreCredentials()
        {
            try
            {
                _accessToken = await _dataStore.GetAsync<string>(nameof(_accessToken));
                _refreshToken = await _dataStore.GetAsync<string>(nameof(_refreshToken));

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public virtual async Task StoreCredentials()
        {
            await _dataStore.StoreAsync(nameof(_accessToken), _accessToken);
            await _dataStore.StoreAsync(nameof(_refreshToken), _refreshToken);
        }


        private protected abstract Task<(bool result, string accessToken, string refreshToken)> OnAuthorizing(Object login);

        public async Task<bool> Authorize(Object login)
        {
            var loginResult = await OnAuthorizing(login);

            if (loginResult.result)
            {
                _accessToken = loginResult.accessToken;
                _refreshToken = loginResult.refreshToken;

                await StoreCredentials();
            }

            return loginResult.result;
        }

        /// <summary>
        /// </summary>
        /// <param name="oldAccessToken"></param>
        /// <param name="oldRefreshToken"></param>
        /// <returns>bool result</returns>
        private protected abstract Task<(bool result, string newAccessToken, string newRefreshToken)> RefreshAccessToken(string oldAccessToken, string oldRefreshToken);

        private async Task<bool> RefreshAccessToken()
        {
            var result = await RefreshAccessToken(_accessToken, _refreshToken);

            _accessToken = result.newAccessToken;
            _refreshToken = result.newRefreshToken;

            await StoreCredentials();

            return result.result;
        }

        public async Task<T> Request<T>(RequestTypes type, TControllersEnum controller, object body = null, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
        {
            using (WebClient request = new WebClient())
            {
                request.ConfigureRequest(_accessToken, Headers);

                string address = UrlBuilder(controller, route);
                switch (type)
                {
                    case RequestTypes.GET:
                        {
                            return JsonConvert.DeserializeObject<T>(
                                await request.DownloadStringTaskAsync(address));
                        }
                    default:
                        {
                            request.Headers.Add(HttpRequestHeader.ContentType, "application/json");

                            return JsonConvert.DeserializeObject<T>(
                                await request.UploadStringTaskAsync(address, type.ToString(),
                                    JsonConvert.SerializeObject(body, Formatting.Indented)));
                        }
                }
            }
        }

        public async Task<byte[]> RequestData(DataRequestTypes type, TControllersEnum controller, byte[] data, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
        {
            using (WebClient request = new WebClient())
            {
                request.ConfigureRequest(_accessToken, Headers);

                string address = UrlBuilder(controller, route);

                if (type == DataRequestTypes.UPLOAD)
                    return await request.UploadDataTaskAsync(address, type.ToString(), data);
                else
                    return await request.DownloadDataTaskAsync(address);
            }
        }

        public async Task<byte[]> RequestData(DataRequestTypes type, TControllersEnum controller, string filePath, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
        {
            using (WebClient request = new WebClient())
            {
                request.ConfigureRequest(_accessToken, Headers);

                string address = UrlBuilder(controller, route);

                if (type == DataRequestTypes.UPLOAD)
                    return await request.UploadFileTaskAsync(address, type.ToString(), filePath);
                else
                {
                    await request.DownloadFileTaskAsync(address, filePath);
                    return null;
                }
            }
        }

        private string UrlBuilder(TControllersEnum controller, params string[] route)
        {
            StringBuilder builder = new StringBuilder(ApiAddress);

            builder.Append('/').Append(controller);

            foreach (var s in route)
                builder.Append('/').Append(s);

            return builder.ToString();
        }

        public enum RequestTypes
        {
            GET,
            POST,
            DELETE,
            PUT,
            PATCH
        }

        public enum DataRequestTypes
        {
            UPLOAD,
            DOWNLOAD
        }
    }

    internal static class ApiClientExtensions
    {
        internal static void ConfigureRequest(this WebClient request, string token, Dictionary<HttpRequestHeader, string> Headers)
        {
            if (token != null)
                request.Headers.Add("Authorization", $"Bearer {token}");

            if (Headers != null)
                foreach (KeyValuePair<HttpRequestHeader, string> header in Headers)
                    request.Headers.Add(header.Key, header.Value);
        }
    }
}
