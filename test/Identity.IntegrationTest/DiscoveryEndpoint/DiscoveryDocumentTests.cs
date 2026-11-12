using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.DiscoveryEndpoint;

[SutProviderCustomize]
public class DiscoveryDocumentTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public DiscoveryDocumentTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        InitializeSharedFactory(_factory);
    }

    [Fact]
    public async Task WellKnownEndpoint_Success()
    {
        var context = await _factory.Server.GetAsync("/.well-known/openid-configuration");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var endpointRoot = body.RootElement;

        // WARNING: Edits to this file should NOT just be made to "get the test to work" they should be made when intentional
        // changes were made to this endpoint and proper testing will take place to ensure clients are backwards compatible
        // or loss of functionality is properly noted.
        await using var fs = File.OpenRead("openid-configuration.json");
        using var knownConfiguration = await JsonSerializer.DeserializeAsync<JsonDocument>(fs);
        var knownConfigurationRoot = knownConfiguration.RootElement;

        AssertHelper.AssertEqualJson(endpointRoot, knownConfigurationRoot);
    }
}
