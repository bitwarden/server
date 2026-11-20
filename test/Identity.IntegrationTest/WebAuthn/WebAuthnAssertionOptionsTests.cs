using Bit.Core.Auth.Models.Api.Response.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Identity.IntegrationTest.WebAuthn;

public class WebAuthnAssertionOptionsTests(IdentityApplicationFactory _factory) : IClassFixture<IdentityApplicationFactory>
{
    [Fact]
    public async Task AssertionOptions_ReturnsTokenAndOptions()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/accounts/webauthn/assertion-options");

        // Assert
        await Assert.SuccessResponseAsync(response);
        var options = await response.Content.ReadFromJsonAsync<WebAuthnLoginAssertionOptionsResponseModel>();
        Assert.NotNull(options);
        Assert.NotEmpty(options.Token);
        Assert.NotNull(options.Options);
        Assert.NotEmpty(options.Options.Challenge);
    }
}
