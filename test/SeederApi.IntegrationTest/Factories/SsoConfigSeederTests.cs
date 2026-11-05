using Bit.Core.Auth.Enums;
using Bit.Seeder.Factories;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class SsoConfigSeederTests
{
    private const string _idpEntityId = "http://localhost:8090/simplesaml/saml2/idp/metadata.php";
    private const string _idpSsoUrl = "http://localhost:8090/simplesaml/saml2/idp/SSOService.php";
    private const string _idpCert = "FAKE-TEST-CERT-BASE64==";
    private const string _oidcAuthority = "https://idp.example/oauth2/default";
    private const string _oidcClientId = "seeder-client-id";
    private const string _oidcClientSecret = "fake-test-client-secret";
    private const string _oidcMetadataAddress = "https://idp.example/oauth2/default/.well-known/openid-configuration";

    [Fact]
    public void Create_ProducesEnabledConfigForOrganization()
    {
        var orgId = Guid.NewGuid();

        var config = SsoConfigSeeder.Create(orgId, _idpEntityId, _idpSsoUrl, _idpCert);

        Assert.Equal(orgId, config.OrganizationId);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void Create_SerializesSamlDataThatRoundTrips()
    {
        var config = SsoConfigSeeder.Create(Guid.NewGuid(), _idpEntityId, _idpSsoUrl, _idpCert);

        var data = config.GetData();

        Assert.Equal(SsoType.Saml2, data.ConfigType);
        Assert.Equal(MemberDecryptionType.MasterPassword, data.MemberDecryptionType);
        Assert.Equal(_idpEntityId, data.IdpEntityId);
        Assert.Equal(_idpSsoUrl, data.IdpSingleSignOnServiceUrl);
        Assert.Equal(_idpCert, data.IdpX509PublicCert);
        Assert.Equal(Saml2BindingType.HttpRedirect, data.IdpBindingType);
        Assert.True(data.SpUniqueEntityId);
    }

    [Fact]
    public void Create_HonorsMemberDecryptionTypeOverride()
    {
        var config = SsoConfigSeeder.Create(
            Guid.NewGuid(), _idpEntityId, _idpSsoUrl, _idpCert,
            MemberDecryptionType.TrustedDeviceEncryption);

        Assert.Equal(MemberDecryptionType.TrustedDeviceEncryption, config.GetData().MemberDecryptionType);
    }

    [Fact]
    public void CreateOidc_AppliesDefaults()
    {
        var orgId = Guid.NewGuid();

        var config = SsoConfigSeeder.CreateOidc(orgId, _oidcAuthority, _oidcClientId, _oidcClientSecret);

        var data = config.GetData();
        Assert.Equal(orgId, config.OrganizationId);
        Assert.True(config.Enabled);
        Assert.Equal(OpenIdConnectRedirectBehavior.RedirectGet, data.RedirectBehavior);
        Assert.True(data.GetClaimsFromUserInfoEndpoint);
        Assert.Equal(MemberDecryptionType.MasterPassword, data.MemberDecryptionType);
        Assert.Null(data.MetadataAddress);
    }

    [Fact]
    public void CreateOidc_SerializesOidcDataThatRoundTrips()
    {
        var config = SsoConfigSeeder.CreateOidc(
            Guid.NewGuid(), _oidcAuthority, _oidcClientId, _oidcClientSecret, _oidcMetadataAddress,
            OpenIdConnectRedirectBehavior.FormPost, getClaimsFromUserInfoEndpoint: false);

        var data = config.GetData();

        Assert.Equal(SsoType.OpenIdConnect, data.ConfigType);
        Assert.Equal(_oidcAuthority, data.Authority);
        Assert.Equal(_oidcClientId, data.ClientId);
        Assert.Equal(_oidcClientSecret, data.ClientSecret);
        Assert.Equal(_oidcMetadataAddress, data.MetadataAddress);
        Assert.Equal(OpenIdConnectRedirectBehavior.FormPost, data.RedirectBehavior);
        Assert.False(data.GetClaimsFromUserInfoEndpoint);
    }

    [Fact]
    public void CreateOidc_HonorsMemberDecryptionTypeOverride()
    {
        var config = SsoConfigSeeder.CreateOidc(
            Guid.NewGuid(), _oidcAuthority, _oidcClientId, _oidcClientSecret,
            memberDecryptionType: MemberDecryptionType.TrustedDeviceEncryption);

        Assert.Equal(MemberDecryptionType.TrustedDeviceEncryption, config.GetData().MemberDecryptionType);
    }
}
