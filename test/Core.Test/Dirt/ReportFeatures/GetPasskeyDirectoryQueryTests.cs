using System.Net;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class GetPasskeyDirectoryQueryTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public GetPasskeyDirectoryQueryTests()
    {
        _handler = new MockedHttpMessageHandler();
        _httpClient = _handler.ToHttpClient();
    }

    private SutProvider<GetPasskeyDirectoryQuery> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(GetPasskeyDirectoryQuery.HttpClientName).Returns(_httpClient);

        var cache = Substitute.For<IFusionCache>();
        cache.GetOrSetAsync(
            key: Arg.Any<string?>(),
            factory: Arg.Any<Func<object, CancellationToken, Task<List<PasskeyDirectoryEntry>>>>(),
            options: Arg.Any<FusionCacheEntryOptions>(),
            tags: Arg.Any<IEnumerable<string>>()
        ).Returns(callInfo =>
        {
            var factory = callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<List<PasskeyDirectoryEntry>>, CancellationToken, Task<List<PasskeyDirectoryEntry>>>>(1);
            return new ValueTask<List<PasskeyDirectoryEntry>>(factory.Invoke(null!, CancellationToken.None));
        });

        return new SutProvider<GetPasskeyDirectoryQuery>()
            .SetDependency(clientFactory)
            .SetDependency(cache)
            .Create();
    }

    [Fact]
    public async Task GetPasskeyDirectoryAsync_ReturnsFilteredEntries()
    {
        var json = """
        {
            "example.com": {
                "passwordless": "allowed",
                "mfa": "allowed",
                "documentation": "https://example.com/help"
            },
            "nopasskey.com": {
                "contact": { "twitter": "nopasskey" }
            },
            "mfaonly.com": {
                "mfa": "required"
            }
        }
        """;

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        var sutProvider = GetSutProvider();
        var result = (await sutProvider.Sut.GetPasskeyDirectoryAsync()).ToList();

        Assert.Equal(2, result.Count);

        var example = result.First(e => e.DomainName == "example.com");
        Assert.True(example.Passwordless);
        Assert.True(example.Mfa);
        Assert.Equal("https://example.com/help", example.Instructions);

        var mfaOnly = result.First(e => e.DomainName == "mfaonly.com");
        Assert.False(mfaOnly.Passwordless);
        Assert.True(mfaOnly.Mfa);
        Assert.Equal(string.Empty, mfaOnly.Instructions);
    }

    [Fact]
    public async Task GetPasskeyDirectoryAsync_EmptyResponse_ReturnsEmpty()
    {
        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        var sutProvider = GetSutProvider();
        var result = (await sutProvider.Sut.GetPasskeyDirectoryAsync()).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPasskeyDirectoryAsync_PasswordlessOnly_ReturnsEntry()
    {
        var json = """
        {
            "passonly.com": {
                "passwordless": "required",
                "documentation": "https://passonly.com/setup"
            }
        }
        """;

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        var sutProvider = GetSutProvider();
        var result = (await sutProvider.Sut.GetPasskeyDirectoryAsync()).ToList();

        Assert.Single(result);
        Assert.Equal("passonly.com", result[0].DomainName);
        Assert.True(result[0].Passwordless);
        Assert.False(result[0].Mfa);
        Assert.Equal("https://passonly.com/setup", result[0].Instructions);
    }

    [Fact]
    public async Task GetPasskeyDirectoryAsync_NoDocumentation_ReturnsEmptyInstructions()
    {
        var json = """
        {
            "nodocs.com": {
                "passwordless": "allowed"
            }
        }
        """;

        _handler.When(HttpMethod.Get)
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        var sutProvider = GetSutProvider();
        var result = (await sutProvider.Sut.GetPasskeyDirectoryAsync()).ToList();

        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Instructions);
    }
}
