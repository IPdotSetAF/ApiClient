using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        #region DataStore

        public virtual async Task<bool> RestoreCredentials()
        {
            try
            {
                _accessToken = await _dataStore.GetAsync<string>(nameof(_accessToken));
                _refreshToken = await _dataStore.GetAsync<string>(nameof(_refreshToken));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring data : {ex.Message}");

                return false;
            }
        }

        public virtual async Task StoreCredentials()
        {
            await _dataStore.StoreAsync(nameof(_accessToken), _accessToken);
            await _dataStore.StoreAsync(nameof(_refreshToken), _refreshToken);
        }

        #endregion

        #region AccessToken

        /// <summary>
        /// Implement the authorization logic here
        /// </summary>
        /// <param name="login">login model</param>
        /// <returns name="result">indicates if the authorization was successful</returns>
        /// <returns name="accessToken">retrived access token</returns>
        /// <returns name="refreshToken">retrived refresh token</returns>
        protected abstract Task<(bool result, string accessToken, string refreshToken)> OnAuthorizing(Object login);

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
        /// Implement the "refreshing access token" logic here
        /// </summary>
        /// <param name="oldAccessToken">previous access token</param>
        /// <param name="oldRefreshToken">previous refresh token</param>
        /// <returns name="result">indicates if the accesstoken refreshing was successful</returns>
        /// <returns name="newAccessToken">retrived access token</returns>
        /// <returns name="newRefreshToken">retrived refresh token</returns>
        protected abstract Task<(bool result, string newAccessToken, string newRefreshToken)> OnRefreshingAccessToken(string oldAccessToken, string oldRefreshToken);

        public async Task<bool> RefreshAccessToken()
        {
            var result = await OnRefreshingAccessToken(_accessToken, _refreshToken);

            if (result.result)
            {
                _accessToken = result.newAccessToken;
                _refreshToken = result.newRefreshToken;

                await StoreCredentials();
            }

            return result.result;
        }

        #endregion

        #region GlobalErrorHandling

        /// <summary>
        /// Used for error handling on all requests.
        /// Global Error handling is enabled by default but can be disabled using request parameters. 
        /// </summary>
        /// <param name="exception">thrown ApiClientException</param>
        /// <returns>return true in order to recall the failed request, false in order to ignore request</returns>
        protected abstract Task<bool> GlobalErrorHandling(ApiClientException exception);

        #endregion

        #region PublicRequests

        public async Task<T> Request<T>(RequestTypes type, TControllersEnum controller, object body = null, Dictionary<HttpRequestHeader, string> Headers = null, bool handleError = true, params string[] route) where T : class
        {
            try
            {
                return await Request<T>(type, controller, body, Headers, route);
            }
            catch (WebException e)
            {
                ApiClientException apiClientException = new ApiClientException(e);

                if (!handleError)
                    throw apiClientException;

                if (await GlobalErrorHandling(apiClientException))
                    return await Request<T>(type, controller, body, Headers, route);
                else return null;
            }
        }

        public async Task<byte[]> RequestData(DataRequestTypes type, TControllersEnum controller, byte[] data, Dictionary<HttpRequestHeader, string> Headers = null, bool handleError = true, params string[] route)
        {
            try
            {
                return await RequestData(type, controller, data, Headers, route);
            }
            catch (WebException e)
            {
                ApiClientException apiClientException = new ApiClientException(e);

                if (!handleError)
                    throw apiClientException;

                if (await GlobalErrorHandling(apiClientException))
                    return await RequestData(type, controller, data, Headers, route);
                else return null;
            }
        }

        public async Task<byte[]> RequestData(DataRequestTypes type, TControllersEnum controller, string filePath, Dictionary<HttpRequestHeader, string> Headers = null, bool handleError = true, params string[] route)
        {
            try
            {
                return await RequestData(type, controller, filePath, Headers, route);
            }
            catch (WebException e)
            {
                ApiClientException apiClientException = new ApiClientException(e);

                if (!handleError)
                    throw apiClientException;

                if (await GlobalErrorHandling(new ApiClientException(e)))
                    return await RequestData(type, controller, filePath, Headers, route);
                else return null;
            }
        }

        #endregion

        #region PrivateRequests

        private async Task<T> Request<T>(RequestTypes type, TControllersEnum controller, object body = null, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
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

        private async Task<byte[]> RequestData(DataRequestTypes type, TControllersEnum controller, byte[] data, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
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

        private async Task<byte[]> RequestData(DataRequestTypes type, TControllersEnum controller, string filePath, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
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

        #endregion

        private string UrlBuilder(TControllersEnum controller, params string[] route)
        {
            StringBuilder builder = new StringBuilder(ApiAddress);

            builder.Append('/').Append(controller);

            foreach (var s in route)
                builder.Append('/').Append(s);

            return builder.ToString();
        }
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
