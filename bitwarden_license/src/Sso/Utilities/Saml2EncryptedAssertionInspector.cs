using System.Xml;
using Sustainsys.Saml2;

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
    /// A SAML response can hold more than one assertion, and each encrypted assertion holds its own key.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response or request.</param>
    /// <returns>
    /// An empty list when the envelope holds no <c>saml:EncryptedAssertion</c> element.
    /// Otherwise, a list with one entry for each distinct value.
    /// An entry holds the algorithm URI when it is a known URI.
    /// An entry holds <c>"unrecognized"</c> when an assertion names an algorithm outside the allow list.
    /// An entry holds null when an assertion names no algorithm.
    /// </returns>
    /// <remarks>
    /// This method runs on the unauthenticated assertion consumer service (ACS) request path.
    /// It must not throw for any XML shape, because a throw blocks single sign-on (SSO) login.
    /// </remarks>
    public static IReadOnlyList<string?> InspectKeyEncryptionAlgorithms(XmlElement envelope)
    {
        // Both Sustainsys and downstream gates respect first children EncryptedAssertion nodes only.
        var encryptedAssertions = envelope.ChildNodes
            .OfType<XmlElement>()
            .Where(e => e.LocalName == "EncryptedAssertion"
                 && e.NamespaceURI == Saml2Namespaces.Saml2Name);

        var algorithms = new List<string?>();
        foreach (XmlElement encryptedAssertion in encryptedAssertions)
        {
            var algorithm = ReadKeyEncryptionAlgorithm(encryptedAssertion);

            // A repeated value adds no information to a log, so each distinct value gets one entry.
            // Contains compares null to null, so an assertion with no algorithm deduplicates like any other.
            if (!algorithms.Contains(algorithm))
            {
                algorithms.Add(algorithm);
            }
        }

        return algorithms;
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
        // The Attributes indexer returns null for a missing attribute, and then throws.
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
