using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Seeder.Factories;

public class OrganizationSeeder
{
    public static Organization CreateFree(string name, string domain, string? publicKey = null, string? privateKey = null)
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            BillingEmail = $"billing@{domain}",
            Plan = "Free",
            PlanType = PlanType.Free,
            Seats = 2,
            UseCustomPermissions = false,
            UseOrganizationDomains = false,
            UseSecretsManager = false,
            UseGroups = false,
            UseDirectory = false,
            UseEvents = false,
            UseTotp = false,
            Use2fa = false,
            UseApi = false,
            UseResetPassword = false,
            UsePasswordManager = true,
            UseAutomaticUserConfirmation = false,
            SelfHost = false,
            UsersGetPremium = false,
            LimitCollectionCreation = true,
            LimitCollectionDeletion = true,
            LimitItemDeletion = true,
            AllowAdminAccessToAllCollectionItems = true,
            UseRiskInsights = false,
            UseAdminSponsoredFamilies = false,
            SyncSeats = false,
            Status = OrganizationStatusType.Created,
            MaxStorageGb = 0,
            PublicKey = publicKey,
            PrivateKey = privateKey
        };
    }

    public static Organization CreateEnterprise(string name, string domain, int seats, string? publicKey = null, string? privateKey = null)
    {
        return CreateEnterprise(name, domain, seats, useSecretsManager: true, publicKey, privateKey);
    }

    public static Organization CreateEnterprise(string name, string domain, int seats, bool useSecretsManager, string? publicKey = null, string? privateKey = null)
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            BillingEmail = $"billing@{domain}",
            Plan = "Enterprise (Annually)",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = seats,
            UseCustomPermissions = true,
            UseOrganizationDomains = true,
            UseSecretsManager = useSecretsManager,
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

public static class OrganizationExtensions
{
    /// <summary>
    /// Creates an OrganizationUser with a dynamically provided encrypted org key.
    /// The encryptedOrgKey should be generated using sdkService.GenerateUserOrganizationKey().
    /// </summary>
    public static OrganizationUser CreateOrganizationUserWithKey(
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
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = shouldLinkUserId ? user.Id : null,
            Email = shouldLinkUserId ? null : user.Email,
            Key = shouldIncludeKey ? encryptedOrgKey : null,
            Type = type,
            Status = status
        };
    }
}
