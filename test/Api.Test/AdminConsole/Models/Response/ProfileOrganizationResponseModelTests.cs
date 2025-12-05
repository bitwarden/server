using Bit.Api.AdminConsole.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class ProfileOrganizationResponseModelTests
{
    [Theory, BitAutoData]
    public void Constructor_ShouldPopulatePropertiesCorrectly(Organization organization)
    {
        var userId = Guid.NewGuid();
        var organizationUserId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var organizationIdsClaimingUser = new[] { organization.Id };

        var organizationDetails = new OrganizationUserOrganizationDetails
        {
            OrganizationId = organization.Id,
            UserId = userId,
            OrganizationUserId = organizationUserId,
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
            UsePhishingBlocker = organization.UsePhishingBlocker,
            UseOrganizationDomains = organization.UseOrganizationDomains,
            UseAdminSponsoredFamilies = organization.UseAdminSponsoredFamilies,
            UseAutomaticUserConfirmation = organization.UseAutomaticUserConfirmation,
            SelfHost = organization.SelfHost,
            Seats = organization.Seats,
            MaxCollections = organization.MaxCollections,
            MaxStorageGb = organization.MaxStorageGb,
            Key = "organization-key",
            PublicKey = "public-key",
            PrivateKey = "private-key",
            LimitCollectionCreation = organization.LimitCollectionCreation,
            LimitCollectionDeletion = organization.LimitCollectionDeletion,
            LimitItemDeletion = organization.LimitItemDeletion,
            AllowAdminAccessToAllCollectionItems = organization.AllowAdminAccessToAllCollectionItems,
            ProviderId = providerId,
            ProviderName = "Test Provider",
            ProviderType = ProviderType.Msp,
            SsoEnabled = true,
            SsoConfig = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector,
                KeyConnectorUrl = "https://keyconnector.example.com"
            }.Serialize(),
            SsoExternalId = "external-sso-id",
            Permissions = CoreHelpers.ClassToJsonData(new Core.Models.Data.Permissions { ManageUsers = true }),
            ResetPasswordKey = "reset-password-key",
            FamilySponsorshipFriendlyName = "Family Sponsorship",
            FamilySponsorshipLastSyncDate = DateTime.UtcNow.AddDays(-1),
            FamilySponsorshipToDelete = false,
            FamilySponsorshipValidUntil = DateTime.UtcNow.AddYears(1),
            IsAdminInitiated = true,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
            AccessSecretsManager = true,
            SmSeats = 5,
            SmServiceAccounts = 10
        };

        var result = new ProfileOrganizationResponseModel(organizationDetails, organizationIdsClaimingUser);

        Assert.Equal("profileOrganization", result.Object);
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
        Assert.Equal(organization.UseAutomaticUserConfirmation, result.UseAutomaticUserConfirmation);
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
        Assert.Equal(organizationDetails.SsoEnabled, result.SsoEnabled);
        Assert.True(result.KeyConnectorEnabled);
        Assert.Equal("https://keyconnector.example.com", result.KeyConnectorUrl);
        Assert.Equal(MemberDecryptionType.KeyConnector, result.SsoMemberDecryptionType);
        Assert.True(result.SsoBound);
        Assert.Equal(organizationDetails.Status, result.Status);
        Assert.Equal(organizationDetails.Type, result.Type);
        Assert.Equal(organizationDetails.OrganizationUserId, result.OrganizationUserId);
        Assert.True(result.UserIsClaimedByOrganization);
        Assert.NotNull(result.Permissions);
        Assert.True(result.ResetPasswordEnrolled);
        Assert.Equal(organizationDetails.AccessSecretsManager, result.AccessSecretsManager);
        Assert.Equal(organizationDetails.FamilySponsorshipFriendlyName, result.FamilySponsorshipFriendlyName);
        Assert.Equal(organizationDetails.FamilySponsorshipLastSyncDate, result.FamilySponsorshipLastSyncDate);
        Assert.Equal(organizationDetails.FamilySponsorshipToDelete, result.FamilySponsorshipToDelete);
        Assert.Equal(organizationDetails.FamilySponsorshipValidUntil, result.FamilySponsorshipValidUntil);
        Assert.True(result.IsAdminInitiated);
        Assert.False(result.FamilySponsorshipAvailable);
    }
}
