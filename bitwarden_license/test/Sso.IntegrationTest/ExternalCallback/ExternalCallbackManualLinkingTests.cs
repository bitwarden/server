using System.Net;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// A request to <c>/Account/ExternalCallback</c> that carries a user identifier links an existing account to
/// SSO by hand. The identifier must have the <c>userId,token</c> form and a valid token, and the identity
/// provider must still send an email claim.
/// </summary>
public class ExternalCallbackManualLinkingTests
{
    /*
    * Test to verify /Account/ExternalCallback returns error when email cannot be found
    * during new user provisioning (Scenario 2) after bypassing the first email check
    * via manual linking path (userIdentifier is set).
    */
    [Fact]
    public async Task ExternalCallback_WithUserIdentifier_AndNoEmail_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUserIdentifier("")
            .WithNullEmail()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because email cannot be found during new user provisioning
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cannot find email claim", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when userIdentifier
    * is malformed (doesn't contain comma separator for userId,token format).
    * There is only a single test case here but in the future we may need to expand the
    * tests to cover other invalid formats.
    */
    [Theory]
    [BitAutoData("No-Comas-Identifier")]
    public async Task ExternalCallback_WithInvalidUserIdentifierFormat_ReturnsError(
        string UserIdentifier
    )
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUserIdentifier(UserIdentifier)
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because userIdentifier format is invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid user identifier", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when userIdentifier
    * contains valid userId but invalid/mismatched token.
    */
    [Fact]
    public async Task ExternalCallback_WithUserIdentifier_AndInvalidToken_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithManualLinkingToken("invalid-token")
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because token verification failed
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("Supplied userId and token did not match", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
