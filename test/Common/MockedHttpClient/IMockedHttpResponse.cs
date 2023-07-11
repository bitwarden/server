namespace Bit.Test.Common.MockedHttpClient;

public interface IMockedHttpResponse
{
    int NumberOfResponses { get; }
    HttpResponseMessage RespondTo(HttpRequestMessage request);
}
