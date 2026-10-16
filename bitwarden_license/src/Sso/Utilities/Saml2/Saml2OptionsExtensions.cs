// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.IO.Compression;
using System.Text;
using System.Xml;
using Bit.Core;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.WebSso;

namespace Bit.Sso.Utilities.Saml2;

public static class Saml2OptionsExtensions
{
    public static async Task<bool> CouldHandleAsync(this Saml2Options options, string scheme, HttpContext context)
    {
        // Determine this is a valid request for our handler
        if (!context.Request.Path.StartsWithSegments(options.SPOptions.ModulePath, StringComparison.Ordinal))
        {
            return false;
        }

        var idp = options.IdentityProviders.IsEmpty ? null : options.IdentityProviders.Default;
        if (idp == null)
        {
            return false;
        }

        var featureService = context.RequestServices.GetRequiredService<IFeatureService>();
        var enforceRsa15Deprecation = featureService.IsEnabled(FeatureFlagKeys.PM42283_Rsa15Deprecation);
        var schemeMatchesQuery = context.Request.Query["scheme"].FirstOrDefault() == scheme;

        if (!enforceRsa15Deprecation && schemeMatchesQuery)
        {
            return true;
        }

        XmlElement envelope;
        if (enforceRsa15Deprecation)
        {
            // The scheme query parameter identifies only a request without a SAML message, such as a metadata
            // or login-initiation request. A request with a message must pass the inspection below.
            try
            {
                var requestData = context.ToHttpRequestData(new NoCookieManager(), _ => null);
                var binding = options.Notifications.GetBinding(requestData);
                if (binding == null)
                {
                    return schemeMatchesQuery;
                }
                // The middleware already rejects the artifact binding. Artifact resolution needs a call to the identity
                // provider, which this check never makes.
                if (binding is Saml2ArtifactBinding)
                {
                    return false;
                }
                envelope = binding.Unbind(requestData, null).Data;
            }
            catch
            {
                return false;
            }
        }
        else
        {
            envelope = await ReadEnvelopeLegacyAsync(context);
        }

        if (envelope == null)
        {
            return false;
        }

        // Double check the entity Ids
        var entityId = envelope["Issuer", Saml2Namespaces.Saml2Name]?.InnerText.Trim();
        if (!string.Equals(entityId, idp.EntityId.Id, StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        // Check and notify before signature verification. A signature failure should not fail notification on an unapproved
        // key transport algorithm. Any request, including a forged, unsigned, or improperly-constructed one 
        // that satisfies the notification criteria, should notify.
        NotifyWhenRsa15Deprecated(envelope, scheme, context);

        // The signature check below decrypts. Reject an unaccepted key transport algorithm first.
        if (enforceRsa15Deprecation)
        {
            Saml2EncryptedAssertionInspector.EnsureAcceptedKeyTransportAlgorithms(envelope);
        }

        if (options.SPOptions.WantAssertionsSigned)
        {
            if (featureService.IsEnabled(FeatureFlagKeys.PM42982_WantAssertionsSigned))
            {
                Saml2AssertionSignatureVerifier.EnsureAssertionsSigned(envelope, options, idp);
            }
            else
            {
                // This can throw if an IdP sends encrypted assertions in an
                // <EncryptedAssertion> node. Both <Assertion> and <EncryptedAssertion> are
                // allowed per OASIS spec in an encrypted case. They are mutually exclusive
                // on a per-assertion basis.
                // PM-42982 exists to improve this site to handle all cases.
                var assertion = envelope["Assertion", Saml2Namespaces.Saml2Name];
                var isAssertionSigned = assertion != null && XmlHelpers.IsSignedByAny(assertion, idp.SigningKeys,
                    options.SPOptions.ValidateCertificates, options.SPOptions.MinIncomingSigningAlgorithm);
                if (!isAssertionSigned)
                {
                    throw new Exception("Cannot verify SAML assertion signature.");
                }
            }
        }

        return true;
    }

    private static async Task<XmlElement> ReadEnvelopeLegacyAsync(HttpContext context)
    {
        try
        {
            if (string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
                context.Request.HasFormContentType)
            {
                string encodedMessage;
                if (context.Request.Form.TryGetValue("SAMLResponse", out var response))
                {
                    encodedMessage = response.FirstOrDefault();
                }
                else
                {
                    encodedMessage = context.Request.Form["SAMLRequest"];
                }
                if (string.IsNullOrWhiteSpace(encodedMessage))
                {
                    return null;
                }
                return XmlHelpers.XmlDocumentFromString(
                    Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage)))?.DocumentElement;
            }

            if (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var encodedPayload = context.Request.Query["SAMLRequest"].FirstOrDefault() ??
                    context.Request.Query["SAMLResponse"].FirstOrDefault();
                try
                {
                    var payload = Convert.FromBase64String(encodedPayload);
                    using var compressed = new MemoryStream(payload);
                    using var decompressedStream = new DeflateStream(compressed, CompressionMode.Decompress, true);
                    using var deCompressed = new MemoryStream();
                    await decompressedStream.CopyToAsync(deCompressed);

                    return XmlHelpers.XmlDocumentFromString(
                        Encoding.UTF8.GetString(deCompressed.GetBuffer(), 0, (int)deCompressed.Length))?.DocumentElement;
                }
                catch (FormatException ex)
                {
                    throw new FormatException($"\'{encodedPayload}\' is not a valid Base64 encoded string: {ex.Message}", ex);
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    // Reading the message needs no cookie. A cookie reader that finds none keeps Sustainsys.Saml2 from
    // decrypting the RelayState cookie.
    private sealed class NoCookieManager : ICookieManager
    {
        public string GetRequestCookie(HttpContext context, string key) => null;

        public void AppendResponseCookie(HttpContext context, string key, string value, CookieOptions options)
        {
        }

        public void DeleteCookie(HttpContext context, string key, CookieOptions options)
        {
        }
    }

    /// <summary>
    /// Queues the RSA 1.5 deprecation notice for the organization when the assertion uses RSA 1.5 key transport.
    /// This method never throws, because a throw blocks single sign-on (SSO) login.
    /// </summary>
    private static void NotifyWhenRsa15Deprecated(XmlElement envelope, string scheme, HttpContext context)
    {
        try
        {
            // This is checked again later inside the command which handles the notification.
            // A value of 0 means "no email is sent," not "send any number of emails without regard
            // for a cooldown." This stands in for a feature flag, and 
            // ensures notification also works for self-hosted instances.
            var globalSettings = context.RequestServices.GetRequiredService<IGlobalSettings>();
            if (globalSettings.Sso.Rsa15DeprecationEmailCooldownInDays <= 0)
            {
                return;
            }

            if (!Saml2EncryptedAssertionInspector.UsesRsa15KeyTransport(envelope) ||
                !Guid.TryParse(scheme, out var organizationId))
            {
                return;
            }

            context.RequestServices.GetRequiredService<ISaml2Rsa15DeprecationNotifier>().TryQueue(organizationId);
        }
        catch
        {
            // The notice is best effort. It must never block login, and it must not log the organization ID.
        }
    }
}
