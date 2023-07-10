using System.Net;

namespace Bit.Test.Common.MockedHttpClient;

public class HttpResponseBuilder
{
    public HttpStatusCode StatusCode { get; set; }
    public IEnumerable<KeyValuePair<string, string>> Headers { get; set; } = new List<KeyValuePair<string, string>>();
    public HttpContent Content { get; set; }

    public HttpResponseMessage ToHttpResponse()
    {
        var message = new HttpResponseMessage(StatusCode)
        {
            Content = Content
        };

        foreach (var header in Headers)
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return message;
    }

    public HttpResponseBuilder WithStatusCode(HttpStatusCode statusCode)
    {
        return new()
        {
            StatusCode = statusCode,
            Headers = Headers,
            Content = Content,
        };
    }

    public HttpResponseBuilder WithHeader(string name, string value)
    {
        return new()
        {
            StatusCode = StatusCode,
            Headers = Headers.Append(new KeyValuePair<string, string>(name, value)),
            Content = Content,
        };
    }

    public HttpResponseBuilder WithContent(HttpContent content)
    {
        return new()
        {
            StatusCode = StatusCode,
            Headers = Headers,
            Content = content,
        };
    }
}
