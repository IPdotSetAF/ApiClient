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
    public abstract class ApiClient<TControllersEnum, TDataStore> where TControllersEnum : Enum where TDataStore : IDataStore, new()
    {
        private const string SerializeKey = "ApiCredentials";

        [JsonIgnore]
        public abstract string ApiAddress { get; set; }

        [JsonProperty("AccessToken")]
        private string _accessToken { get; set; }

        [JsonProperty("RefreshToken")]
        private string _refreshToken { get; set; }


        public async Task<bool> HasAccessToken()
        {
            if (string.IsNullOrEmpty(_accessToken))
                await RestoreCredentials();
            return !String.IsNullOrEmpty(_accessToken);
        }

        public async Task<bool> HasRefreshToken()
        {
            if (string.IsNullOrEmpty(_refreshToken))
                await RestoreCredentials();
            return !String.IsNullOrEmpty(_refreshToken);
        }

        private protected IDataStore _dataStore = new TDataStore();

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

        public async Task<T?> Request<T>(RequestTypes type, TControllersEnum controller, object body = null, Dictionary<HttpRequestHeader, string>? Headers = null, bool handleError = true, RouteBuilder? route = null) where T : class
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

        public async Task<byte[]?> RequestData(DataRequestTypes type, TControllersEnum controller, byte[] data, Dictionary<HttpRequestHeader, string>? Headers = null, bool handleError = true, RouteBuilder? route = null)
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

        public async Task<byte[]?> RequestData(DataRequestTypes type, TControllersEnum controller, string filePath, Dictionary<HttpRequestHeader, string>? Headers = null, bool handleError = true, RouteBuilder? route = null)
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

        private async Task<T?> Request<T>(RequestTypes type, TControllersEnum controller, object body = null, Dictionary<HttpRequestHeader, string>? Headers = null, RouteBuilder? route = null)
        {
            using (WebClient request = new WebClient())
            {
                request.ConfigureRequest(_accessToken, Headers);

                string address = route.BuildUrl(ApiAddress, controller);

                string response;
                switch (type)
                {
                    case RequestTypes.GET:
                        {
                            response = await request.DownloadStringTaskAsync(address);
                            break;
                        }
                    default:
                        {
                            request.Headers.Add(HttpRequestHeader.ContentType, "application/json");

                            response = await request.UploadStringTaskAsync(address, type.ToString(), JsonConvert.SerializeObject(body, Formatting.Indented));

                            break;
                        }
                }

                return JsonConvert.DeserializeObject<T>(response);
            }
        }

        private async Task<byte[]?> RequestData(DataRequestTypes type, TControllersEnum controller, byte[] data, Dictionary<HttpRequestHeader, string>? Headers = null, RouteBuilder? route = null)
        {
            using (WebClient request = new WebClient())
            {
                request.ConfigureRequest(_accessToken, Headers);

                string address = route.BuildUrl(ApiAddress, controller);

                if (type == DataRequestTypes.UPLOAD)
                    return await request.UploadDataTaskAsync(address, type.ToString(), data);
                else
                    return await request.DownloadDataTaskAsync(address);
            }
        }

        private async Task<byte[]?> RequestData(DataRequestTypes type, TControllersEnum controller, string filePath, Dictionary<HttpRequestHeader, string>? Headers = null, RouteBuilder? route=null)
        {
            using (WebClient request = new WebClient())
            {
                request.ConfigureRequest(_accessToken, Headers);

                string address = route.BuildUrl(ApiAddress, controller);

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
