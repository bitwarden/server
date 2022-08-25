using System.Net;

namespace Bit.Scim.Handlers
{
    public class RequestResult
    {
        public bool Success { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public dynamic Data { get; set; }

        public RequestResult(bool success = true)
        {
            Success = success;
        }

        public RequestResult(bool success, HttpStatusCode statusCode, dynamic data = null) : this(success)
        {
            StatusCode = statusCode;
            Data = data;
        }
    }
}
