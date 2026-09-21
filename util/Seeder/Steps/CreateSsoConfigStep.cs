using System.Xml;
using System.Xml.Linq;
using Bit.Core.Auth.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Steps;

/// <summary>
/// Attaches a SAML 2.0 SSO configuration (wired to the local dev IdP) to the organization and sets
/// its SSO identifier. Only SAML is supported today; other providers are skipped rather than persisting
/// a config that cannot authenticate against the bundled IdP. The IdP signing certificate is fetched
/// from the live metadata endpoint at execution time, so no certificate is hardcoded in source.
/// </summary>
internal sealed class CreateSsoConfigStep(
    string identifier,
    string? provider,
    MemberDecryptionType memberDecryptionType) : IAsyncStep
{
    private static readonly XNamespace _ds = "http://www.w3.org/2000/09/xmldsig#";

    public async Task ExecuteAsync(SeederContext context)
    {
        if (!string.Equals(provider ?? "saml", "saml", StringComparison.OrdinalIgnoreCase))
        {
            context.GetLogger<CreateSsoConfigStep>()?
                .LogWarning(
                    "SSO provider '{Provider}' is not supported by the seeder yet (only 'saml'); skipping SsoConfig.",
                    provider);
            return;
        }

        var organization = context.RequireOrganization();

        // The Admin Console sets Organization.Identifier when SSO is saved; replicate that so the
        // identifier typed at login (domain_hint) resolves to this org. Mangle it so --mangle runs stay unique.
        organization.Identifier = context.GetMangler().Mangle(identifier);
        context.SsoIdentifier = organization.Identifier;

        // Fetched after the two mutations above, matching where the inline call used to sit in the
        // argument list: an unreachable IdP must still throw with the identifier already applied.
        var signingCertificate = await FetchIdpSigningCertificateAsync(LocalSamlIdp.EntityId);

        var ssoConfig = SsoConfigSeeder.Create(
            organization.Id,
            LocalSamlIdp.EntityId,
            LocalSamlIdp.SingleSignOnServiceUrl,
            signingCertificate,
            memberDecryptionType);

        context.SsoConfigs.Add(ssoConfig);
    }

    /// <summary>
    /// Reads the IdP's public signing certificate from its live metadata document. Keeps the
    /// (public, image-specific) cert out of source so nothing trips secret/SAST scanners, and tracks the
    /// running image automatically. Requires the local <c>idp</c> container to be running.
    /// </summary>
    private static async Task<string> FetchIdpSigningCertificateAsync(string metadataUrl)
    {
        string metadataXml;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            metadataXml = await client.GetStringAsync(metadataUrl);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the local SAML IdP metadata at {metadataUrl}. " +
                "Is the idp container running?  docker compose --profile idp up -d", ex);
        }

        // Parse with DTDs disabled and no external resolver to avoid XXE.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(metadataXml), settings);
        var metadata = XDocument.Load(reader);

        var certificate = metadata.Descendants(_ds + "X509Certificate").FirstOrDefault()?.Value
            ?? throw new InvalidOperationException($"No <X509Certificate> found in IdP metadata at {metadataUrl}.");

        // The base64 may be wrapped across lines in the XML; strip whitespace before storing.
        return new string(certificate.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
