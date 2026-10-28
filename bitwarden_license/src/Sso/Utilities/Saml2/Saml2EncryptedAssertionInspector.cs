using System.Xml;
using Sustainsys.Saml2;

namespace Bit.Sso.Utilities.Saml2;

/// <summary>
/// Reads the shape of a SAML envelope to find encrypted assertions.
/// This inspector never decrypts an assertion and never returns any part of its content.
/// </summary>
public static class Saml2EncryptedAssertionInspector
{
    private const string _xencNamespace = "http://www.w3.org/2001/04/xmlenc#";
    private const string _dsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public const string KeyTransportAlgorithmNotAcceptedMessage =
        "SAML encrypted assertion key transport algorithm is not accepted.";

    /// <summary>
    /// Determines whether any key of any encrypted assertion in the envelope uses the RSA 1.5 key transport algorithm.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response or request.</param>
    /// <returns><see langword="true"/> when a key uses RSA 1.5. <see langword="false"/> otherwise, or when any exception interrupts the check.</returns>
    /// <remarks>
    /// A SAML response can hold more than one assertion. It is defined in the SAML2.0 Schema Protocol as a choice group
    /// with 0 minimum occurrences, and unbounded maximum occurrences. Mixing both Assertion and EncryptedAssertion
    /// in a single Response is allowed. Each encrypted assertion holds one or more keys.
    /// This method checks every key of every assertion.
    /// This method runs on the unauthenticated assertion consumer service (ACS) request path.
    /// This method never throws, because a throw blocks single sign-on (SSO) login.
    /// </remarks>
    /// <see href="https://docs.oasis-open.org/security/saml/v2.0/saml-schema-protocol-2.0.xsd" />
    public static bool UsesRsa15KeyTransport(XmlElement envelope)
    {
        try
        {
            return ReadEnvelopeKeyEncryptionAlgorithms(envelope)
                .Any(algorithm => string.Equals(
                    algorithm, Saml2KeyTransportEncryptionAlgorithms.Rsa15, StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Throws when a top-level encrypted assertion has no xenc:EncryptedKey, or has any key whose
    /// key transport algorithm is not in <see cref="Saml2KeyTransportEncryptionAlgorithms.Accepted"/>.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response or request.</param>
    /// <remarks>
    /// This method never decrypts an assertion, and it must run before any decryption.
    /// Only OAEP key transport is accepted.
    /// A ds:RetrievalMethod must name an xenc:EncryptedKey in the same encrypted assertion.
    /// Thus, every key that decryption can use is a key that this method checks.
    /// This method does not catch exceptions, so an envelope it cannot read fails closed.
    /// The exception message is constant. It never includes a value from the envelope.
    /// </remarks>
    public static void EnsureAcceptedKeyTransportAlgorithms(XmlElement envelope)
    {
        var encryptedAssertions = envelope.ChildNodes
            .OfType<XmlElement>()
            .Where(e => e.LocalName == "EncryptedAssertion"
                && e.NamespaceURI == Saml2Namespaces.Saml2Name);

        foreach (var encryptedAssertion in encryptedAssertions)
        {
            var algorithms = ReadKeyEncryptionAlgorithms(encryptedAssertion).ToArray();
            if (algorithms.Length == 0 || algorithms.Any(a =>
                    !Saml2KeyTransportEncryptionAlgorithms.Accepted.Contains(a, StringComparer.Ordinal)))
            {
                throw new Exception(KeyTransportAlgorithmNotAcceptedMessage);
            }

            EnsureRetrievalMethodsNameEncryptedKeys(encryptedAssertion);
        }
    }

    private static void EnsureRetrievalMethodsNameEncryptedKeys(XmlElement encryptedAssertion)
    {
        var retrievalMethods = encryptedAssertion
            .GetElementsByTagName("RetrievalMethod", _dsigNamespace)
            .OfType<XmlElement>()
            .ToArray();
        if (retrievalMethods.Length == 0)
        {
            return;
        }

        var elements = encryptedAssertion.GetElementsByTagName("*").OfType<XmlElement>()
            .Prepend(encryptedAssertion)
            .ToArray();

        foreach (var retrievalMethod in retrievalMethods)
        {
            var id = ReadLocalReferenceId(retrievalMethod.GetAttribute("URI"));
            var targets = id == null
                ? []
                : elements.Where(e => HasId(e, id)).ToArray();
            if (targets.Length == 0 || targets.Any(e => !IsEncryptedKey(e)))
            {
                throw new Exception(KeyTransportAlgorithmNotAcceptedMessage);
            }
        }
    }

    private static string? ReadLocalReferenceId(string uri)
    {
        if (uri.Length < 2 || uri[0] != '#')
        {
            return null;
        }

        var id = uri.Substring(1);
        try
        {
            return XmlConvert.VerifyNCName(id);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static bool HasId(XmlElement element, string id) =>
        element.GetAttribute("Id") == id || element.GetAttribute("id") == id || element.GetAttribute("ID") == id;

    private static bool IsEncryptedKey(XmlElement element) =>
        element.LocalName == "EncryptedKey" && element.NamespaceURI == _xencNamespace;

    private static IEnumerable<string?> ReadEnvelopeKeyEncryptionAlgorithms(XmlElement envelope)
    {
        // Only the first-child nodes are relevant. We don't need a recursive check.
        var encryptedAssertions = envelope.ChildNodes
            .OfType<XmlElement>()
            .Where(e => e.LocalName == "EncryptedAssertion"
                && e.NamespaceURI == Saml2Namespaces.Saml2Name);

        return encryptedAssertions.SelectMany(ReadKeyEncryptionAlgorithms);
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
    /// <see href="https://www.w3.org/TR/xmlenc-core1/#sec-ds-RetrievalMethod"/>
    private static IEnumerable<string?> ReadKeyEncryptionAlgorithms(XmlElement encryptedAssertion)
    {
        // Some identity providers place xenc:EncryptedKey beside xenc:EncryptedData instead of inside it.
        // An xenc:ReferenceList then links the two elements.
        // A search by tag name finds the element in either shape. A fixed nested path does not.
        var encryptedKeys = encryptedAssertion
            .GetElementsByTagName("EncryptedKey", _xencNamespace)
            .OfType<XmlElement>()
            .ToArray();

        return encryptedKeys.Select(ReadAlgorithm);
    }

    private static string? ReadAlgorithm(XmlElement encryptedKey)
    {
        // The xenc:EncryptionMethod child of xenc:EncryptedKey names the key encryption algorithm.
        // The xenc:EncryptionMethod child of xenc:EncryptedData names the data encryption algorithm.
        // Read only the first one. The indexer restricts the read to a direct child.
        return encryptedKey["EncryptionMethod", _xencNamespace]?.GetAttribute("Algorithm");
    }
}
