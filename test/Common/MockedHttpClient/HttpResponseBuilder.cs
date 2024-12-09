using System.Net;

namespace Bit.Test.Common.MockedHttpClient;

public class HttpResponseBuilder : IDisposable
{
    private bool _disposedValue;

    public HttpStatusCode StatusCode { get; set; }
    public IEnumerable<KeyValuePair<string, string>> Headers { get; set; } = new List<KeyValuePair<string, string>>();
    public IEnumerable<string> HeadersToRemove { get; set; } = new List<string>();
    public HttpContent Content { get; set; }

    public async Task<HttpResponseMessage> ToHttpResponseAsync()
    {
        var copiedContentStream = new MemoryStream();
        await Content.CopyToAsync(copiedContentStream); // This is important, otherwise the content stream will be disposed when the response is disposed.
        copiedContentStream.Seek(0, SeekOrigin.Begin);
        var message = new HttpResponseMessage(StatusCode)
        {
            Content = new StreamContent(copiedContentStream),
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
            HeadersToRemove = HeadersToRemove,
            Content = Content,
        };
    }

    public HttpResponseBuilder WithHeader(string name, string value)
    {
        return new()
        {
            StatusCode = StatusCode,
            Headers = Headers.Append(new KeyValuePair<string, string>(name, value)),
            HeadersToRemove = HeadersToRemove,
            Content = Content,
        };
    }

    public HttpResponseBuilder WithContent(HttpContent content)
    {
        return new()
        {
            StatusCode = StatusCode,
            Headers = Headers,
            HeadersToRemove = HeadersToRemove,
            Content = content,
        };
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Content?.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
