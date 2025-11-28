namespace Bit.Test.Common.MockedHttpClient;

public interface IMockedHttpResponse
{
    int NumberOfResponses { get; }
    Task<HttpResponseMessage> RespondToAsync(HttpRequestMessage request);
}
