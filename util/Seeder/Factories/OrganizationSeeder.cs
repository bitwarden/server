using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class OrganizationSeeder
{
    internal static Organization Create(string name, string domain, int seats, string? publicKey = null, string? privateKey = null)
    {
        return new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Name = name,
            BillingEmail = $"billing@{domain}",
            Plan = "Enterprise (Annually)",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = seats,
            UseCustomPermissions = true,
            UseOrganizationDomains = true,
            UseSecretsManager = true,
            UseGroups = true,
            UseDirectory = true,
            UseEvents = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            UseResetPassword = true,
            UsePasswordManager = true,
            UseAutomaticUserConfirmation = true,
            SelfHost = true,
            UsersGetPremium = true,
            LimitCollectionCreation = true,
            LimitCollectionDeletion = true,
            LimitItemDeletion = true,
            AllowAdminAccessToAllCollectionItems = true,
            UseRiskInsights = true,
            UseAdminSponsoredFamilies = true,
            SyncSeats = true,
            Status = OrganizationStatusType.Created,
            MaxStorageGb = 10,
            PublicKey = publicKey,
            PrivateKey = privateKey
        };
    }
}

internal static class OrganizationExtensions
{
    /// <summary>
    /// Creates an OrganizationUser with a dynamically provided encrypted org key.
    /// The encryptedOrgKey should be generated using sdkService.GenerateUserOrganizationKey().
    /// </summary>
    internal static OrganizationUser CreateOrganizationUserWithKey(
        this Organization organization,
        User user,
        OrganizationUserType type,
        OrganizationUserStatusType status,
        string? encryptedOrgKey)
    {
        var shouldLinkUserId = status != OrganizationUserStatusType.Invited;
        var shouldIncludeKey = status == OrganizationUserStatusType.Confirmed || status == OrganizationUserStatusType.Revoked;

        return new OrganizationUser
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            UserId = shouldLinkUserId ? user.Id : null,
            Email = shouldLinkUserId ? null : user.Email,
            Key = shouldIncludeKey ? encryptedOrgKey : null,
            Type = type,
            Status = status
        };
    }
}
