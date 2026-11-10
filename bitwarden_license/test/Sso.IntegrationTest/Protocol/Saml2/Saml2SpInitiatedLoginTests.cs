using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Bit.Core.Auth.Models.Data;
using Bit.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Net.Http.Headers;
using Sustainsys.Saml2;
using Xunit;

namespace Bit.Sso.IntegrationTest.Protocol.Saml2;

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
    private const string ExternalCallbackPath = "/Account/ExternalCallback";

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
        var login = await StartLoginAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            BuildSignedAssertion(login, idpCertificate), spCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await PostAcsAsync(arrangement, login, responseXml);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.SeeOther, $"{(int)response.StatusCode} {body}");
        Assert.Equal(ExternalCallbackPath, response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Starts a login on the Saml2 handler of the organization, the same way a challenge from the account
    /// controller does. Reads the AuthnRequest and the RelayState from the form that the handler writes
    /// for the IdP, and reads the RelayState cookie from the response.
    /// </summary>
    private static async Task<StartedLogin> StartLoginAsync(Saml2AcsArrangement arrangement)
    {
        using var scope = arrangement.Factory.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        using var body = new MemoryStream();
        context.Response.Body = body;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/Account/ExternalChallenge";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        var handler = await scope.ServiceProvider.GetRequiredService<IAuthenticationHandlerProvider>()
            .GetHandlerAsync(context, arrangement.OrganizationId.ToString());
        Assert.NotNull(handler);
        await handler.ChallengeAsync(new AuthenticationProperties
        {
            RedirectUri = ExternalCallbackPath,
            Items = { ["scheme"] = arrangement.OrganizationId.ToString() },
        });

        // The organization uses the default HTTP-POST binding, so the handler writes an HTML form that
        // posts the AuthnRequest and the RelayState to the IdP.
        var form = Encoding.UTF8.GetString(body.ToArray());
        var relayState = ReadHiddenInput(form, "RelayState");
        var authnRequest = XmlHelpers.XmlDocumentFromString(Encoding.UTF8.GetString(
            Convert.FromBase64String(ReadHiddenInput(form, "SAMLRequest")))).DocumentElement!;

        var cookieName = StoredRequestState.CookieNameBase + relayState;
        var cookie = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie.OfType<string>().ToList())
            .Single(c => c.Name.Value == cookieName);

        return new StartedLogin(
            RequestId: authnRequest.GetAttribute("ID"),
            AcsUrl: authnRequest.GetAttribute("AssertionConsumerServiceURL"),
            SpEntityId: authnRequest["Issuer", Saml2Namespaces.Saml2Name]!.InnerText,
            RelayState: relayState,
            CookieHeader: $"{cookie.Name}={cookie.Value}");
    }

    private static async Task<HttpResponseMessage> PostAcsAsync(
        Saml2AcsArrangement arrangement, StartedLogin login, string responseXml)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            SsoConfigurationData.BuildSaml2AcsUrl(null, arrangement.OrganizationId.ToString()))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["SAMLResponse"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseXml)),
                ["RelayState"] = login.RelayState,
            }),
        };
        request.Headers.Add(HeaderNames.Cookie, login.CookieHeader);
        return await arrangement.Client.SendAsync(request);
    }

    private static string BuildSignedAssertion(StartedLogin login, X509Certificate2 idpCertificate)
    {
        var now = DateTime.UtcNow;
        var notBefore = XmlConvert.ToString(now.AddMinutes(-1), XmlDateTimeSerializationMode.Utc);
        var issueInstant = XmlConvert.ToString(now, XmlDateTimeSerializationMode.Utc);
        var notOnOrAfter = XmlConvert.ToString(now.AddMinutes(5), XmlDateTimeSerializationMode.Utc);

        var document = XmlHelpers.XmlDocumentFromString(
            "<saml:Assertion xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\" ID=\"_assertion\" " +
            $"Version=\"2.0\" IssueInstant=\"{issueInstant}\">" +
            $"<saml:Issuer>{Saml2AcsPostHarness.IdpEntityId}</saml:Issuer>" +
            "<saml:Subject>" +
            "<saml:NameID Format=\"urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress\">" +
            "user@test.com</saml:NameID>" +
            "<saml:SubjectConfirmation Method=\"urn:oasis:names:tc:SAML:2.0:cm:bearer\">" +
            $"<saml:SubjectConfirmationData InResponseTo=\"{login.RequestId}\" " +
            $"NotOnOrAfter=\"{notOnOrAfter}\" Recipient=\"{login.AcsUrl}\" />" +
            "</saml:SubjectConfirmation>" +
            "</saml:Subject>" +
            $"<saml:Conditions NotBefore=\"{notBefore}\" NotOnOrAfter=\"{notOnOrAfter}\">" +
            $"<saml:AudienceRestriction><saml:Audience>{login.SpEntityId}</saml:Audience></saml:AudienceRestriction>" +
            "</saml:Conditions>" +
            $"<saml:AuthnStatement AuthnInstant=\"{issueInstant}\" SessionIndex=\"_session\">" +
            "<saml:AuthnContext><saml:AuthnContextClassRef>" +
            "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport" +
            "</saml:AuthnContextClassRef></saml:AuthnContext>" +
            "</saml:AuthnStatement>" +
            "</saml:Assertion>");
        document.DocumentElement!.Sign(idpCertificate, includeKeyInfo: false);
        return document.DocumentElement!.OuterXml;
    }

    private static string ReadHiddenInput(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{name}\"\\s+value=\"([^\"]*)\"");
        Assert.True(match.Success, html);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed record StartedLogin(
        string RequestId, string AcsUrl, string SpEntityId, string RelayState, string CookieHeader);
}
