using System.Net;
using System.Text;

namespace Bit.Test.Common.MockedHttpClient;

public class MockedHttpResponse : IMockedHttpResponse
{
    private MockedHttpResponse _childResponse;
    private readonly Func<HttpRequestMessage, HttpResponseBuilder, HttpResponseBuilder> _responder;

    public int NumberOfResponses { get; private set; }

    public MockedHttpResponse(HttpStatusCode statusCode)
    {
        _responder = (_, builder) => builder.WithStatusCode(statusCode);
    }

    private MockedHttpResponse(Func<HttpRequestMessage, HttpResponseBuilder, HttpResponseBuilder> responder)
    {
        _responder = responder;
    }

    public MockedHttpResponse WithStatusCode(HttpStatusCode statusCode)
    {
        return AddChild((_, builder) => builder.WithStatusCode(statusCode));
    }

    public MockedHttpResponse WithHeader(string name, string value)
    {
        return AddChild((_, builder) => builder.WithHeader(name, value));
    }
    public MockedHttpResponse WithHeaders(params KeyValuePair<string, string>[] headers)
    {
        return AddChild((_, builder) => headers.Aggregate(builder, (b, header) => b.WithHeader(header.Key, header.Value)));
    }

    public MockedHttpResponse WithContent(string mediaType, string content)
    {
        return WithContent(new StringContent(content, Encoding.UTF8, mediaType));
    }
    public MockedHttpResponse WithContent(HttpContent content)
    {
        return AddChild((_, builder) => builder.WithContent(content));
    }

    public HttpResponseMessage RespondTo(HttpRequestMessage request)
    {
        return RespondTo(request, new HttpResponseBuilder());
    }

    private HttpResponseMessage RespondTo(HttpRequestMessage request, HttpResponseBuilder currentBuilder)
    {
        NumberOfResponses++;
        var nextBuilder = _responder(request, currentBuilder);
        return _childResponse == null ? nextBuilder.ToHttpResponse() : _childResponse.RespondTo(request, nextBuilder);
    }

    private MockedHttpResponse AddChild(Func<HttpRequestMessage, HttpResponseBuilder, HttpResponseBuilder> responder)
    {
        _childResponse = new MockedHttpResponse(responder);
        return _childResponse;
    }
}
