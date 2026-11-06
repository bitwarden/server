using Bit.Sso.IntegrationTest.Utilities;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

/// <summary>
/// Checks that a login that starts at the service provider (SP) succeeds, with the PM42283_Rsa15Deprecation
/// flag off and on. The response arrives with the RelayState cookie that holds the login state.
/// <c>CouldHandleAsync</c> must accept the response without reading that cookie.
/// After <c>CouldHandleAsync</c>, <see cref="Bit.Sso.Utilities.SsoAuthenticationMiddleware"/> calls the
/// Sustainsys.Saml2 handler for the same request. The handler reads the cookie.
/// The test host mocks the authentication service, so the test starts the login on the handler directly.
/// </summary>
public class Saml2SpInitiatedLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcsPost_SignedOaepEncryptedAssertionThatAnswersTheLoginRequest_RedirectsToExternalCallback(
        bool rsa15DeprecationFlag)
    {
        // Arrange
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        var spCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, rsa15DeprecationFlag: rsa15DeprecationFlag,
            idpCertificate: idpCertificate, spCertificate: spCertificate);
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLogin.BuildSignedAssertion(login, idpCertificate), spCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLogin.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }
}
