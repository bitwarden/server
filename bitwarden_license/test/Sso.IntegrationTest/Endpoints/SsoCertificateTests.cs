using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Azure.Storage.Blobs;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon;
using Bit.SharedWeb.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Bit.Sso.Models;
using Bit.Test.Common.Helpers;
using DotNet.Testcontainers.Builders;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.IdentityModel.Tokens;
using Sustainsys.Saml2;
using Xunit;

namespace Bit.Sso.IntegrationTest.Endpoints;

public class SsoCertificateTests
{
    private const string OidcIdentityClientKey = "test-oidc-identity-client-key";
    private const string AzuriteImage =
        "mcr.microsoft.com/azure-storage/azurite:3.37.0@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5";
    private static readonly XNamespace _md = "urn:oasis:names:tc:SAML:2.0:metadata";
    private static readonly XNamespace _ds = "http://www.w3.org/2000/09/xmldsig#";

    [Fact]
    public async Task Jwks_WithOneIdentityCertificate_PublishesExactlyThatKey()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            factory.UpdateConfiguration(key, value);
        }
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyAsync(client, certificate);
    }

    [Fact]
    public async Task Metadata_WithOneIdentityCertificate_HasOneKeyDescriptorWithNoUse()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var keyDescriptor = Assert.Single(XDocument.Parse(xml).Descendants(_md + "KeyDescriptor"));
        Assert.Null(keyDescriptor.Attribute("use"));
        Assert.Equal(
            Convert.ToBase64String(certificate.RawData),
            keyDescriptor.Descendants(_ds + "X509Certificate").Single().Value.Trim());
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToTheIdentityCertificate_Decrypts()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLogin.BuildSignedAssertion(login, idpCertificate), certificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLogin.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Metadata_WithOneIdentityCertificate_IsSignedWithThatCertificate()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(certificate))
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.AsSelfHosted().BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(IsSignedBy(XmlHelpers.XmlDocumentFromString(xml).DocumentElement!, certificate));
    }

    [Fact]
    public async Task AuthnRequest_WithSigningAlwaysAndOneIdentityCertificate_IsSignedWithThatCertificate()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, selfHosted: true, spSigningBehavior: Saml2SigningBehavior.Always,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));

        // Act
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);

        // Assert
        Assert.True(IsSignedBy(login.AuthnRequest, certificate));
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToAnotherCertificate_Fails()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        using var otherCertificate = TestCertificates.Create("CN=Another Certificate");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await Saml2AcsPostHarness.ArrangeAsync(
            string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: CertificateTestConfiguration.SelfHosted(certificate));
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLogin.BuildSignedAssertion(login, idpCertificate), otherCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);

        // Act
        using var response = await Saml2SpInitiatedLogin.PostAcsAsync(arrangement, login, responseXml);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Encrypted Assertion(s) could not be decrypted using the configured Service Certificate(s).",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Jwks_DevelopmentWithNoCertificate_PublishesOneDeveloperKey()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            factory.UpdateConfiguration(key, value);
        }
        using var client = factory.CreateClient();

        // Act
        var keys = await JwksTestHelpers.GetPublishedKeysAsync(client);

        // Assert
        JwksTestHelpers.AssertIsOneDeveloperKey(keys);
    }

    [Fact]
    public async Task Metadata_DevelopmentWithNoCertificate_HasNoKeyDescriptor()
    {
        // Arrange
        var builder = new SsoTestDataBuilder().WithSsoConfig(cfg => cfg!.SetData(BuildSamlConfiguration()));
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            builder.WithConfiguration(key, value);
        }
        var testData = await builder.BuildAsync();
        using var factory = testData.Factory;
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/saml2/{testData.Organization!.Id}");
        var xml = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = XDocument.Parse(xml);
        Assert.Single(document.Descendants(_md + "SPSSODescriptor"));
        Assert.Empty(document.Descendants(_md + "KeyDescriptor"));
    }

    [Fact]
    public async Task Jwks_WithSsoSigningCertificate_DiffersFromIdentity()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var ssoCertificate = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate)
                     .WithSsoSigningCertificate(ssoCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }
        using var client = factory.CreateClient();

        // Act & Assert
        await JwksTestHelpers.AssertPublishesExactlyAsync(client, ssoCertificate);
    }

    [Fact]
    public async Task Startup_WithSsoSigningCertificate_SamlAndTokenSigningUseTheSameCertificateObject()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var ssoCertificate = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate)
                     .WithSsoSigningCertificate(ssoCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var samlSigning = Assert.Single(factory.GetService<SamlEnvironment>().ServiceCertificates,
            entry => entry.Use == CertificateUse.Signing).Certificate;
        var tokenSigning = await GetTokenSigningCertificateAsync(factory);

        // Assert
        Assert.Equal(ssoCertificate.Thumbprint, samlSigning.Thumbprint);
        Assert.Same(samlSigning, tokenSigning);
        Assert.Same(samlSigning, GetReport(factory, CertificateSourceReport.SsoSigning).Active);
    }

    [Fact]
    public async Task Startup_FallbackForBothSsoRoles_SharesTheIdentityCertificateObject()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var encryption = GetReport(factory, CertificateSourceReport.SsoEncryption);
        var signing = GetReport(factory, CertificateSourceReport.SsoSigning);
        var samlEntry = Assert.Single(factory.GetService<SamlEnvironment>().ServiceCertificates);
        var tokenSigning = await GetTokenSigningCertificateAsync(factory);

        // Assert
        Assert.True(encryption.IsFallback);
        Assert.True(signing.IsFallback);
        Assert.Equal(identityCertificate.Thumbprint, encryption.Active?.Thumbprint);
        Assert.Same(encryption.Active, signing.Active);
        Assert.Equal(CertificateUse.Both, samlEntry.Use);
        Assert.Same(signing.Active, samlEntry.Certificate);
        Assert.Same(signing.Active, tokenSigning);
    }

    [Fact]
    public void Startup_ProductionWithNoSsoServer_FailsOnSsoEncryptionFirst()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.NoCertificate())
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Production",
            "No SSO encryption certificate is configured.");

        // Assert
        Assert.Equal(
            "No SSO encryption certificate is configured. A cloud host must set globalSettings:ssoServer:encryption.",
            exception.Message);
    }

    [Fact]
    public async Task Startup_ProductionWithSsoEncryptionBlobAndNoSsoSigningSource_FailsOnSsoSigning()
    {
        // Arrange
        await using var azurite = new ContainerBuilder(AzuriteImage)
            .WithPortBinding(10000, true)
            .Build();
        await azurite.StartAsync();
        var connectionString =
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            $"BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var certificates = (await new BlobServiceClient(connectionString)
            .CreateBlobContainerAsync("certificates")).Value;
        await certificates.UploadBlobAsync("sso-encryption.pfx", new BinaryData(
            encryption.Export(X509ContentType.Pfx, CertificateTestConfiguration.CertificatePassword)));

        using var factory = new SsoApplicationFactory();
        var configuration = CertificateTestConfiguration.NoCertificate();
        configuration["globalSettings:storage:connectionString"] = connectionString;
        configuration["globalSettings:dataProtection:keyProtectionPolicy"] = "StorageManaged";
        configuration["globalSettings:ssoServer:encryption:blobName"] = "sso-encryption.pfx";
        configuration["globalSettings:ssoServer:encryption:certificatePassword"] =
            CertificateTestConfiguration.CertificatePassword;
        foreach (var (key, value) in configuration)
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Production",
            "No SSO signing certificate is configured.");

        // Assert
        Assert.Equal("No SSO signing certificate is configured. A cloud host must set globalSettings:ssoServer.",
            exception.Message);
    }

    [Fact]
    public async Task Token_WithSsoSigningCertificate_IsSignedByThePublishedSsoKey()
    {
        // Arrange
        using var identityCertificate = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var ssoCertificate = TestCertificates.Create("CN=Bitwarden Test SSO");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identityCertificate)
                     .WithSsoSigningCertificate(ssoCertificate))
        {
            factory.UpdateConfiguration(key, value);
        }
        factory.UpdateConfiguration("globalSettings:oidcIdentityClientKey", OidcIdentityClientKey);
        using var client = factory.CreateClient();
        var redirectUri = $"{factory.GetService<GlobalSettings>().BaseServiceUri.Identity}/signin-oidc";
        var codeVerifier = CryptoRandom.CreateUniqueId(32);
        var code = await StoreOidcIdentityAuthorizationCodeAsync(factory, redirectUri, codeVerifier);

        // Act
        using var response = await client.PostAsync(await GetTokenEndpointPathAsync(client),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OidcConstants.GrantTypes.AuthorizationCode,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
                ["client_id"] = "oidc-identity",
                ["client_secret"] = OidcIdentityClientKey,
            }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await JwksTestHelpers.AssertSignedByPublishedKeyAsync(client,
            tokens.RootElement.GetProperty("id_token").GetString()!, ssoCertificate);
    }

    [Fact]
    public async Task Metadata_SsoRolesSetToTheIdentityCertificate_OneKeyDescriptorWithNoUse()
    {
        // Arrange
        using var certificate = TestCertificates.Create();
        var configuration = CertificateTestConfiguration.SelfHosted(certificate)
            .WithSsoSigningCertificate(certificate)
            .WithSsoEncryptionCertificate(certificate);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal([Key(null, certificate)], KeyDescriptors(xml));
    }

    [Fact]
    public async Task Metadata_SplitCertificates_SigningAndEncryptionKeyDescriptors()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var configuration = CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoEncryptionCertificate(encryption);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal([Key("signing", signing), Key("encryption", encryption)], KeyDescriptors(xml));
    }

    [Fact]
    public async Task Metadata_StagedSigning_PublishedAsSecondSigningKey_MetadataSignedByActive()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var stagedSigning = TestCertificates.Create("CN=Bitwarden Test SSO Staged Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var configuration = CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoStagedSigningCertificate(stagedSigning)
            .WithSsoEncryptionCertificate(encryption);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal(
            [Key("signing", signing), Key("encryption", encryption), Key("signing", stagedSigning)],
            KeyDescriptors(xml));
        var document = XmlHelpers.XmlDocumentFromString(xml).DocumentElement!;
        Assert.True(IsSignedBy(document, signing));
        Assert.False(IsSignedBy(document, stagedSigning));
    }

    [Fact]
    public async Task Metadata_StagedEncryption_NotPublished()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var stagedEncryption = TestCertificates.Create("CN=Bitwarden Test SSO Staged Encryption");
        var configuration = CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoEncryptionCertificate(encryption)
            .WithSsoEncryptionFutureCertificate(stagedEncryption, active: false);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal([Key("signing", signing), Key("encryption", encryption)], KeyDescriptors(xml));
    }

    [Fact]
    public async Task Metadata_FallbackWithIdentityAndSsoStaged_BothPublished_ActiveSigns()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityStaged = TestCertificates.Create("CN=Bitwarden Test Identity Staged");
        using var ssoStaged = TestCertificates.Create("CN=Bitwarden Test SSO Staged");
        var configuration = CertificateTestConfiguration.SelfHosted(identity, identityStaged, futureActive: false)
            .WithSsoStagedSigningCertificate(ssoStaged);

        // Act
        var xml = await GetMetadataAsync(configuration);

        // Assert
        Assert.Equal(
            [Key(null, identity), Key("signing", identityStaged), Key("signing", ssoStaged)],
            KeyDescriptors(xml));
        var document = XmlHelpers.XmlDocumentFromString(xml).DocumentElement!;
        Assert.True(IsSignedBy(document, identity));
        Assert.False(IsSignedBy(document, identityStaged));
        Assert.False(IsSignedBy(document, ssoStaged));
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToActiveSsoEncryptionCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoSigningCertificate(signing)
            .WithSsoEncryptionCertificate(encryption));
        var metadata = await GetMetadataAsync(arrangement);

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, encryption);

        // Assert
        Assert.Equal([Key("encryption", encryption)], KeyDescriptors(metadata).Where(key => key.Use != "signing"));
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToStagedEncryptionCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var stagedEncryption = TestCertificates.Create("CN=Bitwarden Test SSO Staged Encryption");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoEncryptionCertificate(encryption)
            .WithSsoEncryptionFutureCertificate(stagedEncryption, active: false));

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, stagedEncryption);

        // Assert
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_FallbackAfterIdentityStage5Rollback_AssertionEncryptedToIdentityStagedCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var identityNew = TestCertificates.Create("CN=Bitwarden Test Identity New");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate,
            CertificateTestConfiguration.SelfHosted(identity, identityNew, futureActive: false));
        var metadata = await GetMetadataAsync(arrangement);

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, identityNew);

        // Assert
        Assert.Equal([Key(null, identity)], KeyDescriptors(metadata).Where(key => key.Use != "signing"));
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AssertionEncryptedToDecryptionCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test SSO Decryption");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoEncryptionCertificate(encryption)
            .WithSsoDecryptionCertificates(decryption));

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, decryption);

        // Assert
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public async Task Acs_AfterReturnFromStage6_AssertionEncryptedToNewCertificate_Decrypts()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var outgoing = TestCertificates.Create("CN=Bitwarden Test SSO Encryption Outgoing");
        using var newCertificate = TestCertificates.Create("CN=Bitwarden Test SSO Encryption New");
        var idpCertificate = Saml2AcsPostHarness.BuildIdpCertificate();
        using var arrangement = await ArrangeAcsAsync(idpCertificate, CertificateTestConfiguration.SelfHosted(identity)
            .WithSsoEncryptionCertificate(newCertificate)
            .WithSsoEncryptionFutureCertificate(outgoing, active: true)
            .WithSsoDecryptionCertificates(newCertificate));
        var metadata = await GetMetadataAsync(arrangement);

        // Act
        using var response = await PostEncryptedAssertionAsync(arrangement, idpCertificate, newCertificate);

        // Assert
        Assert.Equal([Key("encryption", outgoing)], KeyDescriptors(metadata).Where(key => key.Use != "signing"));
        await Saml2SpInitiatedLogin.AssertSucceededAsync(response);
    }

    [Fact]
    public void Startup_ProductionWithNoSsoServerEncryption_Fails()
    {
        // Arrange
        using var factory = new SsoApplicationFactory();
        var configuration = CertificateTestConfiguration.NoCertificate();
        configuration["globalSettings:ssoServer:blobName"] = "sso.pfx";
        foreach (var (key, value) in configuration)
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act & Assert
        StartupTestHelpers.AssertStartupFails(factory, "Production",
            "No SSO encryption certificate is configured. A cloud host must set globalSettings:ssoServer:encryption.");
    }

    [Fact]
    public void Startup_EncryptionCertificateWithNoPrivateKey_Fails()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var publicOnly = X509CertificateLoader.LoadCertificate(encryption.RawData);
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoEncryptionCertificate(publicOnly))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "SSO encryption");

        // Assert
        Assert.Contains(encryption.Thumbprint, exception.Message);
        Assert.Contains("does not contain a private key", exception.InnerException?.Message);
    }

    [Fact]
    public void Startup_LogsCertificateSources()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.Create("CN=Bitwarden Test SSO Signing");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        var logs = new CollectingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoSigningCertificate(signing)
                     .WithSsoEncryptionCertificate(encryption))
        {
            factory.UpdateConfiguration(key, value);
        }
        factory.ConfigureServices(services => services.AddSingleton<ILoggerFactory>(loggerFactory));

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.Equal(
            [
                (CertificateSourceReport.SsoEncryption, "SsoServer", encryption.Thumbprint),
                (CertificateSourceReport.SsoSigning, "SsoServer", signing.Thumbprint),
            ],
            StartupTestHelpers.GetCertificateSourceEntries(logs)
                .Select(entry => (entry.Values["Role"], entry.Values["Source"], entry.Values["ActiveThumbprint"]))
                .OrderBy(entry => entry.Item1));
    }

    [Fact]
    public void Startup_FallbackWithStagedAndDecryptionCertificates_LogsEachCertificateInItsField()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signingStaged = TestCertificates.Create("CN=Bitwarden Test SSO Signing Staged");
        using var encryptionStaged = TestCertificates.Create("CN=Bitwarden Test SSO Encryption Staged");
        using var decryption = TestCertificates.Create("CN=Bitwarden Test SSO Decryption");
        var logs = new CollectingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoStagedSigningCertificate(signingStaged)
                     .WithSsoEncryptionFutureCertificate(encryptionStaged, active: false)
                     .WithSsoDecryptionCertificates(decryption))
        {
            factory.UpdateConfiguration(key, value);
        }
        factory.ConfigureServices(services => services.AddSingleton<ILoggerFactory>(loggerFactory));

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.Equal(
            [
                (CertificateSourceReport.SsoEncryption, "Identity fallback", identity.Thumbprint,
                    encryptionStaged.Thumbprint, decryption.Thumbprint),
                (CertificateSourceReport.SsoSigning, "Identity fallback", identity.Thumbprint,
                    signingStaged.Thumbprint, "none"),
            ],
            StartupTestHelpers.GetCertificateSourceEntries(logs)
                .Select(entry => (entry.Values["Role"], entry.Values["Source"], entry.Values["ActiveThumbprint"],
                    entry.Values["StagedThumbprints"], entry.Values["RetiredThumbprints"]))
                .OrderBy(entry => entry.Item1));
    }

    [Fact]
    public void Startup_WithEcdsaSsoSigningCertificate_FailsNamingTheSsoRoot()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var signing = TestCertificates.CreateEcdsa("CN=Bitwarden Test SSO Signing ECDSA");
        using var encryption = TestCertificates.Create("CN=Bitwarden Test SSO Encryption");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoSigningCertificate(signing)
                     .WithSsoEncryptionCertificate(encryption))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(StartupTestHelpers.NoRsaKeyMessage("globalSettings:ssoServer", signing.Thumbprint),
            exception.Message);
    }

    [Fact]
    public void Startup_WithEcdsaSsoEncryptionCertificate_FailsNamingTheEncryptionRoot()
    {
        // Arrange
        using var identity = TestCertificates.Create("CN=Bitwarden Test Identity");
        using var encryption = TestCertificates.CreateEcdsa("CN=Bitwarden Test SSO Encryption ECDSA");
        using var factory = new SsoApplicationFactory();
        foreach (var (key, value) in CertificateTestConfiguration.SelfHosted(identity)
                     .WithSsoEncryptionCertificate(encryption))
        {
            factory.UpdateConfiguration(key, value);
        }

        // Act
        var exception = StartupTestHelpers.AssertStartupFails(factory, "Development", "does not have an RSA key");

        // Assert
        Assert.Equal(StartupTestHelpers.NoRsaKeyMessage("globalSettings:ssoServer:encryption", encryption.Thumbprint),
            exception.Message);
    }

    private static async Task<X509Certificate2> GetTokenSigningCertificateAsync(SsoApplicationFactory factory)
    {
        var credential = await factory.GetService<ISigningCredentialStore>().GetSigningCredentialsAsync();
        return Assert.IsType<X509SecurityKey>(credential.Key).Certificate;
    }

    private static CertificateSourceReport GetReport(SsoApplicationFactory factory, string role) =>
        Assert.Single(factory.Services.GetServices<CertificateSourceReport>(), report => report.Role == role);

    private static async Task<string> GetMetadataAsync(Dictionary<string, string?> configuration)
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

    private static Task<string> GetMetadataAsync(Saml2AcsArrangement arrangement) =>
        GetMetadataAsync(arrangement.Client, arrangement.OrganizationId);

    private static async Task<string> GetMetadataAsync(HttpClient client, Guid organizationId)
    {
        using var response = await client.GetAsync($"/saml2/{organizationId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static (string? Use, string Certificate) Key(string? use, X509Certificate2 certificate) =>
        (use, Convert.ToBase64String(certificate.RawData));

    private static IEnumerable<(string? Use, string Certificate)> KeyDescriptors(string metadataXml) =>
        XDocument.Parse(metadataXml).Descendants(_md + "KeyDescriptor")
            .Select(keyDescriptor => (keyDescriptor.Attribute("use")?.Value,
                keyDescriptor.Descendants(_ds + "X509Certificate").Single().Value.Trim()))
            .ToList();

    private static Task<Saml2AcsArrangement> ArrangeAcsAsync(X509Certificate2 idpCertificate,
        Dictionary<string, string?> configuration) =>
        Saml2AcsPostHarness.ArrangeAsync(string.Empty, idpCertificate: idpCertificate, selfHosted: true,
            configuration: configuration);

    private static async Task<HttpResponseMessage> PostEncryptedAssertionAsync(Saml2AcsArrangement arrangement,
        X509Certificate2 idpCertificate, X509Certificate2 encryptionCertificate)
    {
        var login = await Saml2SpInitiatedLogin.StartAsync(arrangement);
        var assertion = Saml2AcsPostHarness.EncryptAssertion(
            Saml2SpInitiatedLogin.BuildSignedAssertion(login, idpCertificate), encryptionCertificate);
        var responseXml = Saml2AcsPostHarness.BuildResponseXml(
            assertion, includeSuccessStatus: true, inResponseTo: login.RequestId);
        return await Saml2SpInitiatedLogin.PostAcsAsync(arrangement, login, responseXml);
    }

    private static async Task<string> StoreOidcIdentityAuthorizationCodeAsync(SsoApplicationFactory factory,
        string redirectUri, string codeVerifier)
    {
        var authorizationCode = new AuthorizationCode
        {
            ClientId = "oidc-identity",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = redirectUri,
            RequestedScopes = [OidcConstants.StandardScopes.OpenId],
            IsOpenId = true,
            CodeChallenge = codeVerifier.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain,
            Subject = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(JwtClaimTypes.Subject, Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.IdentityProvider, "local"),
                new Claim(JwtClaimTypes.AuthenticationTime,
                    DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            ], "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role)),
        };

        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationCodeStore>()
            .StoreAuthorizationCodeAsync(authorizationCode);
    }

    private static async Task<string> GetTokenEndpointPathAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var discovery = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new Uri(discovery.RootElement.GetProperty("token_endpoint").GetString()!).LocalPath;
    }

    private static SsoConfigurationData BuildSamlConfiguration() => new()
    {
        ConfigType = SsoType.Saml2,
        IdpEntityId = "https://idp.example.com",
        IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
        IdpX509PublicCert = CoreHelpers.Base64UrlEncode(Saml2AcsPostHarness.BuildIdpCertificate().RawData),
    };

    private static bool IsSignedBy(XmlElement element, X509Certificate2 certificate)
    {
        var signature = Assert.Single(element.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)
            .Cast<XmlElement>());
        var signedXml = new SignedXml(element);
        signedXml.LoadXml(signature);
        return signedXml.CheckSignature(certificate, verifySignatureOnly: true);
    }
}
