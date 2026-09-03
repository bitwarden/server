using System.Xml;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities;

/// <summary>
/// Reads the shape of a SAML envelope to find encrypted assertions.
/// This inspector never decrypts an assertion and never returns any part of its content.
/// </summary>
public static class Saml2EncryptedAssertionInspector
{
    // Encryption algorithms found outside the known list are categorized an "unrecognized."
    private const string _unrecognizedAlgorithm = "unrecognized";

    private const string _xencNamespace = "http://www.w3.org/2001/04/xmlenc#";

    /// <summary>
    /// Examines which algorithms encrypted the keys of the assertions in the envelope.
    /// Logs when an unaccepted algorithm in in use.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response or request.</param>
    /// <param name="scheme">The scheme provided in the request.</param>
    /// <param name="context">The current request context.</param>
    /// <returns><see langword="false"/> when any exception interrupts the check. Otherwise, <see langword="true"/>.</returns>
    /// <remarks>
    /// A SAML response can hold more than one assertion, and each encrypted assertion holds one or more keys.
    /// Every key of every assertion must be checked.
    /// This method runs on the unauthenticated assertion consumer service (ACS) request path.
    /// It must not throw for any XML shape, because a throw blocks single sign-on (SSO) login.
    /// </remarks>
    public static bool TryLogUnsupportedKeyTransportAlgorithms(XmlElement envelope, string scheme, HttpContext context)
    {
        try
        {
            // Only the first-child nodes are relevant. We don't need a recursive check.
            var encryptedAssertions = envelope.ChildNodes
                .OfType<XmlElement>()
                .Where(e => e.LocalName == "EncryptedAssertion"
                    && e.NamespaceURI == Saml2Namespaces.Saml2Name);

            var unacceptedAlgorithms = encryptedAssertions
                .SelectMany(ReadKeyEncryptionAlgorithms)
                .Where(algorithm => !Saml2KeyTransportEncryptionAlgorithms.Accepted.Contains(algorithm))
                .Distinct()
                .ToArray();

            if (unacceptedAlgorithms.Length > 0)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Saml2Options>>();

                foreach (var unacceptedAlgorithm in unacceptedAlgorithms)
                {
                    logger.LogInformation(
                        "Unsupported SAML key encryption. Scheme: {Scheme}," +
                        "KeyEncryptionAlgorithm: {KeyEncryptionAlgorithm}",
                        scheme, unacceptedAlgorithm);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the key encryption algorithm of every xenc:EncryptedKey in one encrypted assertion.
    /// </summary>
    /// <remarks>
    /// The SAML 2.0 assertion schema declares xenc:EncryptedKey with maxOccurs="unbounded" inside
    /// saml:EncryptedElementType, the type of saml:EncryptedAssertion. One assertion can therefore hold
    /// more than one key. XML Encryption 1.1 section 3.5.3 states that such keys carry the same key value,
    /// "possibly encrypted in different ways or for different recipients", so the algorithms can differ.
    /// Every key must be read. Reading only the first key hides a deprecated algorithm behind an accepted one.
    /// </remarks>
    private static IEnumerable<string?> ReadKeyEncryptionAlgorithms(XmlElement encryptedAssertion)
    {
        // Some identity providers place xenc:EncryptedKey beside xenc:EncryptedData instead of inside it.
        // An xenc:ReferenceList then links the two elements.
        // A search by tag name finds the element in either shape. A fixed nested path does not.
        var encryptedKeys = encryptedAssertion
            .GetElementsByTagName("EncryptedKey", _xencNamespace)
            .OfType<XmlElement>()
            .ToArray();

        // An assertion that names no key relies on an out-of-band agreement. Report it as an absent algorithm.
        if (encryptedKeys.Length == 0)
        {
            return [null];
        }

        return encryptedKeys.Select(ClassifyAlgorithm);
    }

    private static string? ClassifyAlgorithm(XmlElement encryptedKey)
    {
        // The xenc:EncryptionMethod child of xenc:EncryptedKey names the key encryption algorithm.
        // The xenc:EncryptionMethod child of xenc:EncryptedData names the data encryption algorithm.
        // Read only the first one. The indexer restricts the read to a direct child.
        //
        // GetAttribute returns an empty string for a missing attribute.
        var rawAlgorithm = encryptedKey["EncryptionMethod", _xencNamespace]?.GetAttribute("Algorithm");

        if (string.IsNullOrEmpty(rawAlgorithm))
        {
            return null;
        }
        return (Saml2KeyTransportEncryptionAlgorithms.Accepted.Contains(rawAlgorithm) ||
            rawAlgorithm.Equals(Saml2KeyTransportEncryptionAlgorithms.Rsa15)) ?
            rawAlgorithm :
            _unrecognizedAlgorithm;
    }
}
