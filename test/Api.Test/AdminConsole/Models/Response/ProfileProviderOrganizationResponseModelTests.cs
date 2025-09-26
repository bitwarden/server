using Bit.Api.AdminConsole.Models.Response;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class ProfileProviderOrganizationResponseModelTests
{
    [Fact]
    public void Constructor_ShouldPopulatePropertiesCorrectly()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid();

        var organization = new ProviderUserOrganizationDetails
        {
            OrganizationId = organizationId,
            UserId = userId,
            Name = "Test Provider Organization",
            Enabled = true,
            PlanType = PlanType.EnterpriseAnnually,
            ProviderId = providerId,
            ProviderName = "Test MSP Provider",
            ProviderType = ProviderType.Msp,
            SsoEnabled = true,
            SsoConfig = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption
            }.Serialize(),
            Status = ProviderUserStatusType.Confirmed,
            Type = ProviderUserType.ProviderAdmin,
            ProviderUserId = providerUserId
        };

        var result = new ProfileProviderOrganizationResponseModel(organization);

        Assert.Equal("profileProviderOrganization", result.Object);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal("Test Provider Organization", result.Name);
        Assert.True(result.Enabled);
        Assert.Equal(ProductTierType.Enterprise, result.ProductTierType);
        Assert.Equal(providerId, result.ProviderId);
        Assert.Equal("Test MSP Provider", result.ProviderName);
        Assert.Equal(ProviderType.Msp, result.ProviderType);
        Assert.True(result.SsoEnabled);
        Assert.False(result.KeyConnectorEnabled);
        Assert.Null(result.KeyConnectorUrl);
        Assert.Equal(MemberDecryptionType.TrustedDeviceEncryption, result.SsoMemberDecryptionType);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result.Status);
        Assert.Equal(OrganizationUserType.Owner, result.Type);
        Assert.False(result.SsoBound);
        Assert.NotNull(result.Permissions);
        Assert.False(result.ResetPasswordEnrolled);
        Assert.Null(result.OrganizationUserId);
        Assert.False(result.UserIsClaimedByOrganization);
        Assert.False(result.UseSecretsManager);
        Assert.False(result.UsePasswordManager); // Provider model doesn't have this data
        Assert.False(result.AccessSecretsManager); // Provider model doesn't have this data
        Assert.Null(result.FamilySponsorshipFriendlyName);
        Assert.False(result.FamilySponsorshipAvailable);
        Assert.False(result.IsAdminInitiated);
    }
}
