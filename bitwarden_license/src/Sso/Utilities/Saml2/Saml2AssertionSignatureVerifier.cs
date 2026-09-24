using System.Xml;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities.Saml2;

public static class Saml2AssertionSignatureVerifier
{
    /// <summary>
    /// Enforces the organization's "want assertions signed" setting against an inbound envelope.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response or request.</param>
    /// <param name="options">The resolved options of the organization's scheme.</param>
    /// <param name="idp">The identity provider the envelope claims to come from.</param>
    /// <exception cref="Exception">
    /// The envelope does not carry the signed assertions it requires.
    /// </exception>
    public static void EnsureAssertionsSigned(XmlElement envelope, Saml2Options options, IdentityProvider idp)
    {
        // Assertions only appear in a <Response>. Single logout uses the same path
        // as the assertion consumer service, so <LogoutRequest> and <LogoutResponse> messages
        // reach this method too.
        // Both LogoutRequest and LogoutResponse use the POST and the Redirect binding. 
        // Neither carries an assertion, and this check must not reject them.
        // <see href="https://docs.oasis-open.org/security/saml/v2.0/saml-schema-protocol-2.0.xsd" />
        if (!IsAuthnResponse(envelope))
        {
            return;
        }

        var assertionElements = GetAssertionElements(envelope);

        // An identity provider that refuses to authenticate returns an error status and no
        // assertions per section 4.1.4.2 of the OASIS spec. That message has nothing to check.
        // <see href="https://docs.oasis-open.org/security/saml/v2.0/saml-profiles-2.0-os.pdf" />
        if (assertionElements.Length == 0 && HasErrorStatus(envelope))
        {
            return;
        }

        var hasUnsignedAssertion = assertionElements
            .Any(element => !IsSignedByIdentityProvider(element, options, idp));

        if (assertionElements.Length == 0 || hasUnsignedAssertion)
        {
            throw new Exception("Cannot verify SAML assertion signature.");
        }
    }

    public static bool IsAuthnResponse(XmlElement envelope) =>
        envelope.LocalName == "Response" && envelope.NamespaceURI == Saml2Namespaces.Saml2PName;

    /// <summary>
    /// Collects every assertion in an envelope in either encrypted or plaintext form.
    /// </summary>
    /// <remarks>
    /// A response can carry more than one assertion, for example from a federation proxy that
    /// aggregates identity providers. Every one must be checked. Per the OASIS spec the two forms
    /// are mutually exclusive for a given assertion, so an encrypted assertion never also appears
    /// as an <c>&lt;Assertion&gt;</c> node.
    /// </remarks>
    /// <see href="https://docs.oasis-open.org/security/saml/v2.0/saml-schema-protocol-2.0.xsd"/>
    public static XmlElement[] GetAssertionElements(XmlElement envelope) =>
        envelope.ChildNodes
            .OfType<XmlElement>()
            .Where(e => e.NamespaceURI == Saml2Namespaces.Saml2Name &&
                (e.LocalName == "Assertion" || e.LocalName == "EncryptedAssertion"))
            .ToArray();

    /// <summary>
    /// Verifies one assertion element against the identity provider's signing keys.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the assertion cannot be decrypted.
    /// An assertion this service provider cannot read never counts as signed.
    /// </returns>
    public static bool IsSignedByIdentityProvider(XmlElement element, Saml2Options options,
        IdentityProvider idp)
    {
        // Decrypt first, so this inspects the same content the identity provider actually signed,
        // whether it arrived encrypted or not.
        var assertion = element.LocalName == "Assertion"
            ? element
            : Saml2EncryptedAssertionDecryptor.TryDecryptAssertion(
                element, options.SPOptions.DecryptionServiceCertificates);

        return assertion != null && XmlHelpers.IsSignedByAny(assertion, idp.SigningKeys,
            options.SPOptions.ValidateCertificates, options.SPOptions.MinIncomingSigningAlgorithm);
    }

    /// <summary>
    /// Reads the top-level status code of a SAML envelope.
    /// </summary>
    /// <param name="envelope">The root element of a SAML response.</param>
    /// <returns>
    /// <see langword="true"/> only when the envelope carries a status code that is not Success.
    /// A missing, empty, or unreadable status code returns <see langword="false"/>, so an absent
    /// element is never read as a failed authentication.
    /// </returns>
    public static bool HasErrorStatus(XmlElement envelope)
    {
        // A second-level status code nests inside the top-level one. Only the outer value
        // classifies the response, so this reads the first child and does not recurse.
        var statusCode = envelope["Status", Saml2Namespaces.Saml2PName]
            ?["StatusCode", Saml2Namespaces.Saml2PName]
            ?.GetAttribute("Value")
            .Trim();

        return !string.IsNullOrEmpty(statusCode) && statusCode != Saml2ResponseTypes.SuccessStatus;
    }
}
