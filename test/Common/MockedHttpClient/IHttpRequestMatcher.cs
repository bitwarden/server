#nullable enable

namespace Bit.Test.Common.MockedHttpClient;

public interface IHttpRequestMatcher
{
    int NumberOfMatches { get; }
    bool Matches(HttpRequestMessage request);
    HttpResponseMessage RespondTo(HttpRequestMessage request);
}
