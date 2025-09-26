using Bit.Api.AdminConsole.Models.Response;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class ProfileOrganizationResponseModelTests
{
    [Fact]
    public void Constructor_ShouldPopulatePropertiesCorrectly()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationUserId = Guid.NewGuid();
        var organizationIdsClaimingUser = new[] { organizationId };

        var organization = new OrganizationUserOrganizationDetails
        {
            OrganizationId = organizationId,
            UserId = userId,
            OrganizationUserId = organizationUserId,
            Name = "Test Organization",
            Enabled = true,
            PlanType = PlanType.EnterpriseAnnually,
            SsoEnabled = true,
            SsoConfig = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
                KeyConnectorUrl = "https://keyconnector.example.com"
            }.Serialize(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            SsoExternalId = "external-sso-id",
            Permissions = CoreHelpers.ClassToJsonData(new Core.Models.Data.Permissions { ManageUsers = true }),
            ResetPasswordKey = "reset-password-key",
            UseSecretsManager = true,
            UsePasswordManager = true,
            AccessSecretsManager = true
        };

        var result = new ProfileOrganizationResponseModel(organization, organizationIdsClaimingUser);

        Assert.Equal("profileOrganization", result.Object);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal("Test Organization", result.Name);
        Assert.True(result.Enabled);
        Assert.Equal(ProductTierType.Enterprise, result.ProductTierType);
        Assert.True(result.SsoEnabled);
        Assert.True(result.KeyConnectorEnabled);
        Assert.Equal("https://keyconnector.example.com", result.KeyConnectorUrl);
        Assert.Equal(MemberDecryptionType.KeyConnector, result.SsoMemberDecryptionType);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result.Status);
        Assert.Equal(OrganizationUserType.Owner, result.Type);
        Assert.True(result.SsoBound);
        Assert.NotNull(result.Permissions);
        Assert.True(result.ResetPasswordEnrolled);
        Assert.Equal(organizationUserId, result.OrganizationUserId);
        Assert.True(result.UserIsClaimedByOrganization);
        Assert.True(result.UseSecretsManager);
        Assert.True(result.UsePasswordManager);
        Assert.True(result.AccessSecretsManager);
    }
}
