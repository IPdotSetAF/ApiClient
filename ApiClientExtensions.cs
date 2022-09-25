using System.Net;
using System.Text;

namespace ApiClient
{
    internal static class ApiClientExtensions
    {
        internal static void ConfigureRequest(this WebClient request, string token, Dictionary<HttpRequestHeader, string>? Headers)
        {
            if (token != null)
                request.Headers.Add("Authorization", $"Bearer {token}");

            if (Headers != null)
                foreach (KeyValuePair<HttpRequestHeader, string> header in Headers)
                    request.Headers.Add(header.Key, header.Value);
        }

        internal static string BuildUrl<TControllersEnum>(this RouteBuilder? route, string ApiAddress, TControllersEnum controller) where TControllersEnum : Enum
        {
            StringBuilder builder = new StringBuilder(ApiAddress);

            builder.Append('/').Append(controller);

            builder.Append(route?.ToString());

            return builder.ToString();
        }
    }
}
