using Bit.Api.AdminConsole.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class ProfileProviderOrganizationResponseModelTests
{
    [Theory, BitAutoData]
    public void Constructor_ShouldPopulatePropertiesCorrectly(Organization organization)
    {
        var userId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid();

        var organizationDetails = new ProviderUserOrganizationDetails
        {
            OrganizationId = organization.Id,
            UserId = userId,
            Name = organization.Name,
            Enabled = organization.Enabled,
            Identifier = organization.Identifier,
            PlanType = organization.PlanType,
            UsePolicies = organization.UsePolicies,
            UseSso = organization.UseSso,
            UseKeyConnector = organization.UseKeyConnector,
            UseScim = organization.UseScim,
            UseGroups = organization.UseGroups,
            UseDirectory = organization.UseDirectory,
            UseEvents = organization.UseEvents,
            UseTotp = organization.UseTotp,
            Use2fa = organization.Use2fa,
            UseApi = organization.UseApi,
            UseResetPassword = organization.UseResetPassword,
            UseSecretsManager = organization.UseSecretsManager,
            UsePasswordManager = organization.UsePasswordManager,
            UsersGetPremium = organization.UsersGetPremium,
            UseCustomPermissions = organization.UseCustomPermissions,
            UseRiskInsights = organization.UseRiskInsights,
            UseOrganizationDomains = organization.UseOrganizationDomains,
            UseAdminSponsoredFamilies = organization.UseAdminSponsoredFamilies,
            SelfHost = organization.SelfHost,
            Seats = organization.Seats,
            MaxCollections = organization.MaxCollections,
            MaxStorageGb = organization.MaxStorageGb,
            Key = "provider-org-key",
            PublicKey = "public-key",
            PrivateKey = "private-key",
            LimitCollectionCreation = organization.LimitCollectionCreation,
            LimitCollectionDeletion = organization.LimitCollectionDeletion,
            LimitItemDeletion = organization.LimitItemDeletion,
            AllowAdminAccessToAllCollectionItems = organization.AllowAdminAccessToAllCollectionItems,
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

        var result = new ProfileProviderOrganizationResponseModel(organizationDetails);

        Assert.Equal("profileProviderOrganization", result.Object);
        Assert.Equal(organization.Id, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(organization.Name, result.Name);
        Assert.Equal(organization.Enabled, result.Enabled);
        Assert.Equal(organization.Identifier, result.Identifier);
        Assert.Equal(organization.PlanType.GetProductTier(), result.ProductTierType);
        Assert.Equal(organization.UsePolicies, result.UsePolicies);
        Assert.Equal(organization.UseSso, result.UseSso);
        Assert.Equal(organization.UseKeyConnector, result.UseKeyConnector);
        Assert.Equal(organization.UseScim, result.UseScim);
        Assert.Equal(organization.UseGroups, result.UseGroups);
        Assert.Equal(organization.UseDirectory, result.UseDirectory);
        Assert.Equal(organization.UseEvents, result.UseEvents);
        Assert.Equal(organization.UseTotp, result.UseTotp);
        Assert.Equal(organization.Use2fa, result.Use2fa);
        Assert.Equal(organization.UseApi, result.UseApi);
        Assert.Equal(organization.UseResetPassword, result.UseResetPassword);
        Assert.Equal(organization.UseSecretsManager, result.UseSecretsManager);
        Assert.Equal(organization.UsePasswordManager, result.UsePasswordManager);
        Assert.Equal(organization.UsersGetPremium, result.UsersGetPremium);
        Assert.Equal(organization.UseCustomPermissions, result.UseCustomPermissions);
        Assert.Equal(organization.PlanType.GetProductTier() == ProductTierType.Enterprise, result.UseActivateAutofillPolicy);
        Assert.Equal(organization.UseRiskInsights, result.UseRiskInsights);
        Assert.Equal(organization.UseOrganizationDomains, result.UseOrganizationDomains);
        Assert.Equal(organization.UseAdminSponsoredFamilies, result.UseAdminSponsoredFamilies);
        Assert.Equal(organization.SelfHost, result.SelfHost);
        Assert.Equal(organization.Seats, result.Seats);
        Assert.Equal(organization.MaxCollections, result.MaxCollections);
        Assert.Equal(organization.MaxStorageGb, result.MaxStorageGb);
        Assert.Equal(organizationDetails.Key, result.Key);
        Assert.True(result.HasPublicAndPrivateKeys);
        Assert.Equal(organization.LimitCollectionCreation, result.LimitCollectionCreation);
        Assert.Equal(organization.LimitCollectionDeletion, result.LimitCollectionDeletion);
        Assert.Equal(organization.LimitItemDeletion, result.LimitItemDeletion);
        Assert.Equal(organization.AllowAdminAccessToAllCollectionItems, result.AllowAdminAccessToAllCollectionItems);
        Assert.Equal(organizationDetails.ProviderId, result.ProviderId);
        Assert.Equal(organizationDetails.ProviderName, result.ProviderName);
        Assert.Equal(organizationDetails.ProviderType, result.ProviderType);
        Assert.Equal(OrganizationUserStatusType.Confirmed, result.Status);
        Assert.Equal(OrganizationUserType.Owner, result.Type);
        Assert.Equal(organizationDetails.SsoEnabled, result.SsoEnabled);
        Assert.False(result.KeyConnectorEnabled);
        Assert.Null(result.KeyConnectorUrl);
        Assert.Equal(MemberDecryptionType.TrustedDeviceEncryption, result.SsoMemberDecryptionType);
        Assert.False(result.SsoBound);
        Assert.NotNull(result.Permissions);
        Assert.False(result.Permissions.ManageUsers);
        Assert.False(result.ResetPasswordEnrolled);
        Assert.False(result.AccessSecretsManager);
        Assert.Equal(organizationDetails.FamilySponsorshipFriendlyName, result.FamilySponsorshipFriendlyName);
        Assert.Equal(organizationDetails.FamilySponsorshipLastSyncDate, result.FamilySponsorshipLastSyncDate);
        Assert.Equal(organizationDetails.FamilySponsorshipToDelete, result.FamilySponsorshipToDelete);
        Assert.Equal(organizationDetails.FamilySponsorshipValidUntil, result.FamilySponsorshipValidUntil);
        Assert.False(result.FamilySponsorshipAvailable);
        Assert.False(result.IsAdminInitiated);
    }
}
