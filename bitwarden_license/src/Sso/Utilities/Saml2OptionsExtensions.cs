// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities;

public static class Saml2OptionsExtensions
{
    // Sustainsys.Saml2 2.11.0 has no public API to decrypt a <saml:EncryptedAssertion> outside its
    // own response-processing pipeline; SPOptions.WantAssertionsSigned is a metadata-only flag in
    // this library version and is never consulted during validation.
    // This reflects into the same internal helper the library itself uses
    // (Saml2Response.RetrieveAssertionElements) for consistency.
    // A future Sustainsys.Saml2 upgrade that removes or renames this
    // member throws here, at first use, instead of silently no-op'ing the signature check.
    private static readonly MethodInfo DecryptAssertionMethod =
        typeof(XmlHelpers).Assembly.GetType("Sustainsys.Saml2.Internal.CryptographyExtensions", throwOnError: true)
            .GetMethod("Decrypt", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(XmlElement), typeof(AsymmetricAlgorithm) }, null)
        ?? throw new MissingMethodException("Sustainsys.Saml2.Internal.CryptographyExtensions",
            "Decrypt(XmlElement, AsymmetricAlgorithm)");

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

        if (context.Request.Query["scheme"].FirstOrDefault() == scheme)
        {
            return true;
        }

        XmlElement envelope = null;
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
                    return false;
                }
                envelope = XmlHelpers.XmlDocumentFromString(
                    Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage)))?.DocumentElement;
            }
            else if (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
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

                    envelope = XmlHelpers.XmlDocumentFromString(
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
            return false;
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

        Saml2EncryptedAssertionInspector.TryRecordUnsupportedKeyTransportAlgorithms(envelope, context);

        // A response can carry more than one assertion, e.g. a federation proxy aggregating
        // identity providers.
        // Every <Assertion> and <EncryptedAssertion> must be checked.
        // <Assertion> and <EncryptedAssertion> are mutually exclusive per assertion per the OASIS spec:
        // an encrypted assertion never appears as an <Assertion> node.
        // Decrypt it first, so this check inspects the same content the identity provider actually signed,
        // whether it arrived encrypted or not.
        if (options.SPOptions.WantAssertionsSigned)
        {
            var assertionElements = envelope.ChildNodes
                .OfType<XmlElement>()
                .Where(e => e.NamespaceURI == Saml2Namespaces.Saml2Name &&
                    (e.LocalName == "Assertion" || e.LocalName == "EncryptedAssertion"))
                .ToArray();

            var allAssertionsSigned = assertionElements.Length > 0 && assertionElements.All(element =>
            {
                var assertion = element.LocalName == "Assertion"
                    ? element
                    : TryDecryptAssertion(element, options.SPOptions.DecryptionServiceCertificates);
                return assertion != null && XmlHelpers.IsSignedByAny(assertion, idp.SigningKeys,
                    options.SPOptions.ValidateCertificates, options.SPOptions.MinIncomingSigningAlgorithm);
            });

            if (!allAssertionsSigned)
            {
                throw new Exception("Cannot verify SAML assertion signature.");
            }
        }

        return true;
    }

    // Mirrors Saml2Response.RetrieveAssertionElements: try each configured decryption
    // certificate in turn, since a service provider can have more than one during rotation.
    private static XmlElement TryDecryptAssertion(XmlElement encryptedAssertion,
        IEnumerable<X509Certificate2> decryptionCertificates)
    {
        foreach (var certificate in decryptionCertificates)
        {
            var privateKey = certificate.GetRSAPrivateKey();
            if (privateKey == null)
            {
                continue;
            }

            try
            {
                var decrypted = (XmlElement)DecryptAssertionMethod.Invoke(
                    null, new object[] { encryptedAssertion, privateKey });
                return decrypted["Assertion", Saml2Namespaces.Saml2Name];
            }
            catch (TargetInvocationException ex) when (ex.InnerException is CryptographicException)
            {
                // This certificate could not decrypt the assertion. Try the next one.
            }
        }

        return null;
    }
}
