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

    /// <summary>
    /// Examines which algorithms encrypted the keys of the assertions in the envelope.
    /// Logs when an unaccepted algorithm in in use.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response or request.</param> 
    /// <param name="scheme">The scheme provided in the request.</param>
    /// <param name="context">The current request context.</param>
    /// <remarks>
    /// A SAML response can hold more than one assertion, and each encrypted assertion holds its own key.
    /// Each assertion must be checked.
    /// This method runs on the unauthenticated assertion consumer service (ACS) request path.
    /// It must not throw for any XML shape, because a throw blocks single sign-on (SSO) login.
    /// </remarks>
    public static void InspectKeyEncryptionAlgorithms(XmlElement envelope, string scheme, HttpContext context)
    {
        // Only the first-child nodes are relevant. We don't need a recursive check.
        var encryptedAssertions = envelope.ChildNodes
            .OfType<XmlElement>()
            .Where(e => e.LocalName == "EncryptedAssertion"
                && e.NamespaceURI == Saml2Namespaces.Saml2Name);

        var algorithms = encryptedAssertions
            .Select(ReadKeyEncryptionAlgorithm);

        var unacceptedAlgorithms = algorithms
            .Where(algorithm => !Saml2KeyTransportEncryptionAlgorithms.Accepted.Contains(algorithm))
            .Distinct();

        if (!unacceptedAlgorithms.Any())
        {
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILogger<Saml2Options>>();

        foreach (var unacceptedAlgorithm in unacceptedAlgorithms)
        {
            logger.LogInformation(
                "Unsupported SAML key encryption. Scheme: {Scheme}," +
                "KeyEncryptionAlgorithm: {KeyEncryptionAlgorithm}",
                scheme, unacceptedAlgorithm);
        }
    }

    private static string? ReadKeyEncryptionAlgorithm(XmlElement encryptedAssertion)
    {
        const string XencNamespace = "http://www.w3.org/2001/04/xmlenc#";

        // Some identity providers place xenc:EncryptedKey beside xenc:EncryptedData instead of inside it.
        // An xenc:ReferenceList then links the two elements.
        // A search by tag name finds the element in either shape. A fixed nested path does not.
        var encryptedKeys = encryptedAssertion.GetElementsByTagName("EncryptedKey", XencNamespace);
        var encryptedKey = encryptedKeys.Count > 0 ? encryptedKeys[0] : null;

        // The xenc:EncryptionMethod child of xenc:EncryptedKey names the key encryption algorithm.
        // The xenc:EncryptionMethod child of xenc:EncryptedData names the data encryption algorithm.
        // Read only the first one. The indexer restricts the read to a direct child.
        //
        // GetAttribute returns an empty string for a missing attribute.
        var rawAlgorithm = encryptedKey?["EncryptionMethod", XencNamespace]?.GetAttribute("Algorithm");

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
