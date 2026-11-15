using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Sso.IntegrationTest.Utilities;

/// <summary>
/// Arrange and read helpers that the certificate integration tests share: SAML metadata retrieval and parsing.
/// </summary>
public static class SsoCertificateTestHelpers
{
    public static readonly XNamespace MetadataNamespace = "urn:oasis:names:tc:SAML:2.0:metadata";
    public static readonly XNamespace XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public static async Task<string> GetMetadataAsync(Dictionary<string, string?> configuration)
    {
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in configuration)
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();
        return await GetMetadataAsync(client, testData.Organization!.Id);
    }

    public static Task<string> GetMetadataAsync(Saml2AcsArrangement arrangement) =>
        GetMetadataAsync(arrangement.Client, arrangement.OrganizationId);

    public static async Task<string> GetMetadataAsync(HttpClient client, Guid organizationId)
    {
        using var response = await client.GetAsync($"/saml2/{organizationId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    public static (string? Use, string Certificate) Key(string? use, X509Certificate2 certificate) =>
        (use, Convert.ToBase64String(certificate.RawData));

    public static IEnumerable<(string? Use, string Certificate)> KeyDescriptors(string metadataXml) =>
        XDocument.Parse(metadataXml).Descendants(MetadataNamespace + "KeyDescriptor")
            .Select(keyDescriptor => (keyDescriptor.Attribute("use")?.Value,
                keyDescriptor.Descendants(XmlDsigNamespace + "X509Certificate").Single().Value.Trim()))
            .ToList();

    public static SsoConfigurationData BuildSamlConfiguration() => new()
    {
        ConfigType = SsoType.Saml2,
        IdpEntityId = "https://idp.example.com",
        IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
        IdpX509PublicCert = CoreHelpers.Base64UrlEncode(Saml2AcsPostHarness.BuildIdpCertificate().RawData),
    };
}
