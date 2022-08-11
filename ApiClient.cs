using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ApiClient
{
    public abstract class ApiClient<TControllersEnum> where TControllersEnum : Enum
    {
        public abstract string ApiAddress { get; set; }

        private string _accessToken, _refreshToken;

        protected async Task<TResponse> Athorize<TResponse>(TControllersEnum controller, object body, Func<TResponse, string> accessToken, Func<TResponse, string> refreshToken, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
        {
            TResponse response = await Request<TResponse>(RequestTypes.POST, controller, body, Headers, route);

            _accessToken = accessToken.Invoke(response);
            _refreshToken = refreshToken.Invoke(response);

            return response;
        }

        protected async Task<TResponse> RefreshAccessToken<TResponse>(TControllersEnum controller, object body, Func<TResponse, string> accessToken, Func<TResponse, string> refreshToken, Dictionary<HttpRequestHeader, string> Headers = null, params string[] route)
        {
            TResponse response = await Request<TResponse>(RequestTypes.POST, controller, body, Headers, route);

            _accessToken = accessToken.Invoke(response);
            _refreshToken = refreshToken.Invoke(response);

            return response;
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

    internal static class ApiManagerExtensions
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
