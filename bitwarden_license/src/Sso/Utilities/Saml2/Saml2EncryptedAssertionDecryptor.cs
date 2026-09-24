using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Sustainsys.Saml2;

namespace Bit.Sso.Utilities;

// Centralizes decryption of a <saml:EncryptedAssertion>. Sustainsys.Saml2 2.11.0 exposes no
// public API for this outside its own response-processing pipeline.
public static class Saml2EncryptedAssertionDecryptor
{
    // This reflects into the same internal helper the library itself uses
    // (Saml2Response.RetrieveAssertionElements), for consistency with its own decrypt path.
    // A future Sustainsys.Saml2 upgrade that removes or renames this member throws here,
    // codified in Saml2OptionsExtensions tests, instead of silently no-op'ing the signature check.
    private static readonly MethodInfo DecryptMethod =
        // throwOnError raises a TypeLoadException for an absent type, so the result is never null here.
        typeof(XmlHelpers).Assembly.GetType("Sustainsys.Saml2.Internal.CryptographyExtensions", throwOnError: true)!
            .GetMethod("Decrypt", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(XmlElement), typeof(AsymmetricAlgorithm) }, null)
        ?? throw new MissingMethodException("Sustainsys.Saml2.Internal.CryptographyExtensions",
            "Decrypt(XmlElement, AsymmetricAlgorithm)");

    // Mirrors Saml2Response.RetrieveAssertionElements: try each configured decryption
    // certificate in turn, since a service provider can have more than one during rotation.
    public static XmlElement? TryDecryptAssertion(XmlElement encryptedAssertion,
        IEnumerable<X509Certificate2> decryptionCertificates)
    {
        foreach (var certificate in decryptionCertificates)
        {
            // Generally, ACS operations must fail gracefully.
            // But a failure here would indicate a certificate/configuration issue;
            // similar to DecryptMethod, a failure here should be loud.
            using (var privateKey = certificate.GetRSAPrivateKey())
            {
                if (privateKey == null)
                {
                    continue;
                }

                try
                {
                    // Decrypt declares a non-nullable XmlElement return, so the invocation result is never null.
                    var decrypted = (XmlElement)DecryptMethod.Invoke(
                        null, new object[] { encryptedAssertion, privateKey })!;

                    // Decryption moves every top-level node of the plaintext into the
                    // <EncryptedAssertion>. The assertion can be at any depth, and it can have siblings.
                    // Saml2Response.RetrieveAssertionElements builds claims from the first descendant
                    // <Assertion> in document order. The signature check must read that same element.
                    return (XmlElement?)decrypted
                        .GetElementsByTagName("Assertion", Saml2Namespaces.Saml2Name)
                        .Item(0);
                }
                catch (TargetInvocationException)
                {
                    // This certificate could not decrypt the assertion. Try the next one.
                }
            }
        }

        return null;
    }
}
