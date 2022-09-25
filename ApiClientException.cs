using System.Diagnostics;
using Newtonsoft.Json;
using System.Net;
using System.Runtime.Serialization;

namespace ApiClient
{
    public class ApiClientException : WebException
    {
        public ApiClientException(WebException webException) : base(webException.Message, webException.InnerException, webException.Status, webException.Response)
        {
        }

        protected ApiClientException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }

        public ApiClientException(string message) : base(message)
        {
        }

        public ApiClientException(string message, System.Exception innerException) : base(message, innerException)
        {
        }

        public ApiClientException(string message, System.Exception innerException, WebExceptionStatus status, WebResponse response) : base(message, innerException, status, response)
        {
        }

        public ApiClientException(string message, WebExceptionStatus status) : base(message, status)
        {
        }

        [JsonIgnore]
        public HttpStatusCode? StatusCode
        {
            get
            {
                try
                {
                    //still throws nullrefrence exception if request fails (internet maybe)
                    return ((HttpWebResponse) base.Response).StatusCode;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    return null;
                }
            }
        }

        //does not return just the body
        [JsonIgnore]
        public new string Response
        {
            get
            {
                using (StreamReader sr = new StreamReader(base.Response.GetResponseStream()))
                {
                    return sr.ReadToEnd();
                }
            }
        }


        public T? GetResponse<T>() where T : class
        {
            return JsonConvert.DeserializeObject<T>(Response);
        }
    }
}
