using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class ProviderUserSeederTests
{
    [Fact]
    public void CreateConfirmedAdmin_ProducesConfirmedProviderAdminWithWrappedKey()
    {
        var providerKey = RustSdkService.GenerateOrganizationKeys().Key;
        var ownerKeys = RustSdkService.GenerateUserKeys("owner@provider.test", "asdfasdfasdf");
        var owner = new User { Id = CoreHelpers.GenerateComb(), PublicKey = ownerKeys.PublicKey };
        var provider = ProviderSeeder.Create("Acme MSP", "acme-msp.test", ProviderType.Msp, new NoOpManglerService());

        var providerUser = ProviderUserSeeder.CreateConfirmedAdmin(provider, owner, providerKey);

        Assert.NotEqual(default, providerUser.Id);
        Assert.Equal(provider.Id, providerUser.ProviderId);
        Assert.Equal(owner.Id, providerUser.UserId);
        Assert.Null(providerUser.Email);
        Assert.Equal(ProviderUserType.ProviderAdmin, providerUser.Type);
        Assert.Equal(ProviderUserStatusType.Confirmed, providerUser.Status);
        Assert.False(string.IsNullOrEmpty(providerUser.Key));
    }

    [Theory]
    // Invited: no UserId link (only an email invitation), no key.
    [InlineData(ProviderUserStatusType.Invited, false, true, false)]
    // Accepted: UserId linked, but the membership is not yet confirmed so no key.
    [InlineData(ProviderUserStatusType.Accepted, true, false, false)]
    // Confirmed: UserId linked and the provider key wrapped for the member.
    [InlineData(ProviderUserStatusType.Confirmed, true, false, true)]
    public void CreateProviderUser_MapsStatusToUserIdEmailAndKey(
        ProviderUserStatusType status,
        bool expectUserId,
        bool expectEmail,
        bool expectKey)
    {
        var provider = ProviderSeeder.Create("Acme MSP", "acme-msp.test", ProviderType.Msp, new NoOpManglerService());
        var user = new User
        {
            Id = CoreHelpers.GenerateComb(),
            Email = "member@provider.test",
            PublicKey = "public-key"
        };
        const string encryptedProviderKey = "encrypted-provider-key";

        var providerUser = ProviderUserSeeder.CreateProviderUser(
            provider,
            user,
            ProviderUserType.ServiceUser,
            status,
            encryptedProviderKey);

        Assert.NotEqual(default, providerUser.Id);
        Assert.Equal(provider.Id, providerUser.ProviderId);
        Assert.Equal(ProviderUserType.ServiceUser, providerUser.Type);
        Assert.Equal(status, providerUser.Status);

        if (expectUserId)
        {
            Assert.Equal(user.Id, providerUser.UserId);
        }
        else
        {
            Assert.Null(providerUser.UserId);
        }

        if (expectEmail)
        {
            Assert.Equal(user.Email, providerUser.Email);
        }
        else
        {
            Assert.Null(providerUser.Email);
        }

        if (expectKey)
        {
            Assert.Equal(encryptedProviderKey, providerUser.Key);
        }
        else
        {
            Assert.Null(providerUser.Key);
        }
    }
}
