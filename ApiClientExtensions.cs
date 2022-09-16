using System.Net;

namespace ApiClient
{
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
