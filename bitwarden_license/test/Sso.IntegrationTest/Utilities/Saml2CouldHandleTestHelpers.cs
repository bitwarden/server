using System.Security.Cryptography.X509Certificates;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Authentication;
using Sustainsys.Saml2.AspNetCore2;
using Xunit;
using static Bit.Sso.IntegrationTest.Utilities.Saml2MessageBuilder;

namespace Bit.Sso.IntegrationTest.Utilities;

/// <summary>
/// Builds a real, database-backed SAML organization scheme and the request context that tests pass to
/// <c>Saml2Options.CouldHandleAsync</c>.
/// </summary>
public static class Saml2CouldHandleTestHelpers
{
    public static (X509Certificate2 IdpCertificate, X509Certificate2 SpCertificate) BuildCertificates() =>
        (CreateSelfSignedCertificate("CN=Test IdP"), CreateSelfSignedCertificate("CN=Test SP"));

    // Builds the real, database-backed scheme and returns the pieces each arrangement needs.
    // The callers exercise the multi-assertion signature verifier
    // (Saml2AssertionSignatureVerifier.EnsureAssertionsSigned), so PM42982_WantAssertionsSigned
    // is enabled by default. A test that wants the legacy, pre-flag branch instead must pass
    // featureFlagEnabled: false.
    public static async Task<(SsoTestData TestData, Saml2Options SamlOptions, string OrganizationId)>
        BuildSchemeAsync(X509Certificate2 idpCertificate, X509Certificate2 spCertificate,
            bool wantAssertionsSigned, bool featureFlagEnabled = true, int? rsa15EmailCooldownInDays = null,
            bool rsa15DeprecationFlag = false)
    {
        var builder = new SsoTestDataBuilder()
            .WithSsoConfig(cfg => cfg!.SetData(new SsoConfigurationData
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = IdpEntityId,
                IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
                IdpSingleLogoutServiceUrl = "https://idp.example.com/slo",
                IdpX509PublicCert = CoreHelpers.Base64UrlEncode(idpCertificate.RawData),
                SpWantAssertionsSigned = wantAssertionsSigned,
            }))
            .WithSamlSigningCertificate(spCertificate)
            .WithPM42982WantAssertionsSignedFlag(featureFlagEnabled)
            .WithPM42283Rsa15DeprecationFlag(rsa15DeprecationFlag);
        if (rsa15EmailCooldownInDays.HasValue)
        {
            builder.WithRsa15DeprecationEmailCooldownInDays(rsa15EmailCooldownInDays.Value);
        }
        var testData = await builder.BuildAsync();

        var organizationId = testData.Organization!.Id.ToString();
        var scheme = await testData.Factory.Services.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(organizationId);
        var dynamicScheme = Assert.IsType<DynamicAuthenticationScheme>(scheme);
        var samlOptions = Assert.IsType<Saml2Options>(dynamicScheme.Options);

        return (testData, samlOptions, organizationId);
    }

    public static async Task<Saml2CouldHandleArrangement> ArrangeAsync(string assertionElement,
        X509Certificate2 idpCertificate, X509Certificate2 spCertificate, bool wantAssertionsSigned,
        bool featureFlagEnabled = true, bool rsa15DeprecationFlag = false)
    {
        var (testData, samlOptions, organizationId) = await BuildSchemeAsync(
            idpCertificate, spCertificate, wantAssertionsSigned, featureFlagEnabled,
            rsa15DeprecationFlag: rsa15DeprecationFlag);

        var context = BuildPostContext(testData,
            SsoConfigurationData.BuildSaml2AcsUrl(null, organizationId),
            "SAMLResponse",
            BuildResponseXml(assertionElement));

        return new Saml2CouldHandleArrangement(samlOptions, organizationId, context);
    }
}

/// <summary>
/// The SAML options, the scheme name, and the request context that <c>CouldHandleAsync</c> receives.
/// </summary>
public sealed record Saml2CouldHandleArrangement(Saml2Options SamlOptions, string Scheme, HttpContext Context);
