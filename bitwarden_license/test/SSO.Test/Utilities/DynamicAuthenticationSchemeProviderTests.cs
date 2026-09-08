using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Business.Sso;
using Bit.Core.Utilities;
using Bit.Sso.Models;
using Bit.Sso.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Metadata;

namespace Bit.SSO.Test.Utilities;

public class DynamicAuthenticationSchemeProviderTests
{
    [Fact]
    public void OnMetadataCreated_NonSigningKeyExists_AddsAcceptedAlgorithmsInOrder()
    {
        var keyDescriptor = new KeyDescriptor { Use = KeyType.Unspecified };
        var entityDescriptor = BuildEntityDescriptor(keyDescriptor);

        DynamicAuthenticationSchemeProvider.OnMetadataCreated(entityDescriptor, null!);

        Assert.Equal(
            SamlEncryptionAlgorithms.Accepted,
            keyDescriptor.EncryptionMethods.Select(m => m.Algorithm.ToString()));
    }

    [Fact]
    public void OnMetadataCreated_NonSigningKeyExists_AddsAlgorithmsInDocumentedLiteralOrder()
    {
        var keyDescriptor = new KeyDescriptor { Use = KeyType.Unspecified };
        var entityDescriptor = BuildEntityDescriptor(keyDescriptor);

        DynamicAuthenticationSchemeProvider.OnMetadataCreated(entityDescriptor, null!);

        // rsa-oaep-mgf1p must come first; IdPs generally choose the first advertised method.
        Assert.Equal(
            new[]
            {
                "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p",
                "http://www.w3.org/2009/xmlenc11#rsa-oaep",
            },
            keyDescriptor.EncryptionMethods.Select(m => m.Algorithm.ToString()));
    }

    [Fact]
    public void OnMetadataCreated_OnlySigningKeyExists_AddsNoEncryptionMethods()
    {
        var signingKey = new KeyDescriptor { Use = KeyType.Signing };
        var entityDescriptor = BuildEntityDescriptor(signingKey);

        DynamicAuthenticationSchemeProvider.OnMetadataCreated(entityDescriptor, null!);

        Assert.Empty(signingKey.EncryptionMethods);
    }

    [Fact]
    public void OnMetadataCreated_NoSpSsoDescriptor_DoesNotThrow()
    {
        var entityDescriptor = new EntityDescriptor();

        var exception = Record.Exception(() =>
            DynamicAuthenticationSchemeProvider.OnMetadataCreated(entityDescriptor, null!));

        Assert.Null(exception);
    }

    [Fact]
    public void OnMetadataCreated_MultipleNonSigningKeys_AllKeysGetMethods()
    {
        var firstKey = new KeyDescriptor { Use = KeyType.Encryption };
        var secondKey = new KeyDescriptor { Use = KeyType.Unspecified };
        var spSsoDescriptor = new SpSsoDescriptor();
        spSsoDescriptor.Keys.Add(firstKey);
        spSsoDescriptor.Keys.Add(secondKey);
        var entityDescriptor = new EntityDescriptor();
        entityDescriptor.RoleDescriptors.Add(spSsoDescriptor);

        DynamicAuthenticationSchemeProvider.OnMetadataCreated(entityDescriptor, null!);

        Assert.Equal(
            SamlEncryptionAlgorithms.Accepted,
            firstKey.EncryptionMethods.Select(m => m.Algorithm.ToString()));
        Assert.Equal(
            SamlEncryptionAlgorithms.Accepted,
            secondKey.EncryptionMethods.Select(m => m.Algorithm.ToString()));
    }

    [Theory, BitAutoData]
    public async Task GetSchemeAsync_Saml2Config_WiresUpMetadataCreatedNotification(
        Guid organizationId,
        SutProvider<DynamicAuthenticationSchemeProvider> sutProvider)
    {
        sutProvider.SetDependency<IOptions<AuthenticationOptions>>(
            Options.Create(new AuthenticationOptions()));
        sutProvider.SetDependency<IOptionsMonitorCache<Saml2Options>>(
            Substitute.For<IExtendedOptionsMonitorCache<Saml2Options>>());
        sutProvider.SetDependency<IOptionsMonitorCache<OpenIdConnectOptions>>(
            Substitute.For<IExtendedOptionsMonitorCache<OpenIdConnectOptions>>());
        sutProvider.SetDependency(new SamlEnvironment());
        sutProvider.Create();

        using var idpSigningKey = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        using var idpSigningCertificate = new CertificateRequest(
            "CN=Test IdP signing certificate", idpSigningKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));

        var ssoConfig = new SsoConfig { OrganizationId = organizationId, Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = "https://idp.example.com",
            IdpSingleSignOnServiceUrl = "https://idp.example.com/sso",
            IdpX509PublicCert = CoreHelpers.Base64UrlEncode(idpSigningCertificate.RawData),
        });
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organizationId)
            .Returns(ssoConfig);

        var scheme = await sutProvider.Sut.GetSchemeAsync(organizationId.ToString());

        var saml2Options = Assert.IsType<Saml2Options>(((DynamicAuthenticationScheme)scheme).Options);
        Assert.Contains(
            saml2Options.Notifications.MetadataCreated.GetInvocationList(),
            d => d.Method.Name == nameof(DynamicAuthenticationSchemeProvider.OnMetadataCreated));
    }

    private static EntityDescriptor BuildEntityDescriptor(KeyDescriptor keyDescriptor)
    {
        var spSsoDescriptor = new SpSsoDescriptor();
        spSsoDescriptor.Keys.Add(keyDescriptor);
        var entityDescriptor = new EntityDescriptor();
        entityDescriptor.RoleDescriptors.Add(spSsoDescriptor);
        return entityDescriptor;
    }
}
