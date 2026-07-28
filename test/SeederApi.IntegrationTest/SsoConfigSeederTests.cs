using Bit.Core.Auth.Enums;
using Bit.Seeder.Factories;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class SsoConfigSeederTests
{
    private const string _idpEntityId = "http://localhost:8090/simplesaml/saml2/idp/metadata.php";
    private const string _idpSsoUrl = "http://localhost:8090/simplesaml/saml2/idp/SSOService.php";
    private const string _idpCert = "FAKE-TEST-CERT-BASE64==";

    [Fact]
    public void CreateSaml2_ProducesEnabledConfigForOrganization()
    {
        var orgId = Guid.NewGuid();

        var config = SsoConfigSeeder.CreateSaml2(orgId, _idpEntityId, _idpSsoUrl, _idpCert);

        Assert.Equal(orgId, config.OrganizationId);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void CreateSaml2_SerializesSamlDataThatRoundTrips()
    {
        var config = SsoConfigSeeder.CreateSaml2(Guid.NewGuid(), _idpEntityId, _idpSsoUrl, _idpCert);

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
    public void CreateSaml2_HonorsMemberDecryptionTypeOverride()
    {
        var config = SsoConfigSeeder.CreateSaml2(
            Guid.NewGuid(), _idpEntityId, _idpSsoUrl, _idpCert,
            MemberDecryptionType.TrustedDeviceEncryption);

        Assert.Equal(MemberDecryptionType.TrustedDeviceEncryption, config.GetData().MemberDecryptionType);
    }
}
