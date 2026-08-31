using System.Text;
using Bit.Sso.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;

namespace Bit.SSO.Test.Utilities;

public class Saml2OptionsExtensionsTests
{
    private const string Scheme = "test-scheme";
    private const string ModulePath = "/saml2/test-scheme";
    private const string IdpEntityId = "https://idp.example.com/metadata";

    [Fact]
    public async Task CouldHandleAsync_EncryptedAssertionAndWantAssertionsSigned_DoesNotThrow()
    {
        // An encrypted assertion has no plaintext <saml:Assertion> element at this stage.
        // There is no element to check for a signature.
        // A throw here blocks single sign-on (SSO) login for each organization that sets
        // WantAssertionsSigned to true and receives encrypted assertions.
        var options = BuildOptions(wantAssertionsSigned: true);
        var context = BuildPostContext(BuildResponseXml(
            "<saml:EncryptedAssertion>" +
            "<xenc:EncryptedData xmlns:xenc=\"http://www.w3.org/2001/04/xmlenc#\">" +
            "<xenc:CipherData><xenc:CipherValue>Y2lwaGVydGV4dA==</xenc:CipherValue></xenc:CipherData>" +
            "</xenc:EncryptedData>" +
            "</saml:EncryptedAssertion>"));

        Assert.True(await options.CouldHandleAsync(Scheme, context));
    }

    [Fact]
    public async Task CouldHandleAsync_NoAssertionAndWantAssertionsSigned_Throws()
    {
        // An envelope with no <saml:Assertion> element and no <saml:EncryptedAssertion> element
        // must still cause a throw from the signature check.
        var options = BuildOptions(wantAssertionsSigned: true);
        var context = BuildPostContext(BuildResponseXml(string.Empty));

        var exception = await Assert.ThrowsAsync<Exception>(
            () => options.CouldHandleAsync(Scheme, context));
        Assert.Equal("Cannot verify SAML assertion signature.", exception.Message);
    }

    private static Saml2Options BuildOptions(bool wantAssertionsSigned)
    {
        var spOptions = new SPOptions
        {
            EntityId = new EntityId("https://sso.bitwarden.com" + ModulePath),
            ModulePath = ModulePath,
            WantAssertionsSigned = wantAssertionsSigned,
        };
        // This test does not configure a signing certificate.
        // Neither test case calls XmlHelpers.IsSignedByAny.
        // If the test sets LoadMetadata to true, IdentityProvider.Validate() then requires a certificate.
        var idp = new IdentityProvider(new EntityId(IdpEntityId), spOptions)
        {
            Binding = Saml2BindingType.HttpPost,
            SingleSignOnServiceUrl = new Uri("https://idp.example.com/sso"),
        };

        var options = new Saml2Options { SPOptions = spOptions };
        options.IdentityProviders.Add(idp);
        return options;
    }

    private static string BuildResponseXml(string assertionElement) =>
        "<samlp:Response xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\" " +
        "xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" " +
        "ID=\"_response\" Version=\"2.0\" IssueInstant=\"2026-01-01T00:00:00Z\">" +
        $"<saml:Issuer>{IdpEntityId}</saml:Issuer>" +
        assertionElement +
        "</samlp:Response>";

    private static HttpContext BuildPostContext(string responseXml)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = ModulePath + "/Acs";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
        });
        return context;
    }
}
