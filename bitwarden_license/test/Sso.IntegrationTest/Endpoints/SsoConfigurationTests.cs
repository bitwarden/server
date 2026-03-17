using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

public class SsoConfigurationTests : IClassFixture<SsoApplicationFactory>
{
    private readonly SsoApplicationFactory _factory;

    public SsoConfigurationTests(SsoApplicationFactory factory)
    {
        _factory = factory;
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
        var knownConfigurationRoot = knownConfiguration!.RootElement;

        AssertHelper.AssertEqualJson(endpointRoot, knownConfigurationRoot);
    }
}
