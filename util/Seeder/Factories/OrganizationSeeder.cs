using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Seeder.Factories;

public class OrganizationSeeder
{
    /// <summary>
    /// Creates an enterprise organization without encryption keys.
    /// Keys should be generated dynamically using RustSdkService.GenerateOrganizationKeys()
    /// and assigned to PublicKey/PrivateKey after creation.
    /// </summary>
    public static Organization CreateEnterprise(string name, string domain, int seats)
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
            // PublicKey and PrivateKey intentionally not set - caller must generate and assign
        };
    }
}

public static class OrganizationExtensions
{
    /// <summary>
    /// Creates an OrganizationUser with hardcoded keys (no SDK calls).
    /// Used by OrganizationWithUsersRecipe for fast user creation without encryption needs.
    /// </summary>
    public static OrganizationUser CreateOrganizationUser(
        this Organization organization, User user, OrganizationUserType type, OrganizationUserStatusType status)
    {
        var isInvited = status == OrganizationUserStatusType.Invited;
        var isConfirmed = status == OrganizationUserStatusType.Confirmed || status == OrganizationUserStatusType.Revoked;

        return new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = isInvited ? null : user.Id,
            Email = isInvited ? user.Email : null,
            Key = isConfirmed ? "4.rY01mZFXHOsBAg5Fq4gyXuklWfm6mQASm42DJpx05a+e2mmp+P5W6r54WU2hlREX0uoTxyP91bKKwickSPdCQQ58J45LXHdr9t2uzOYyjVzpzebFcdMw1eElR9W2DW8wEk9+mvtWvKwu7yTebzND+46y1nRMoFydi5zPVLSlJEf81qZZ4Uh1UUMLwXz+NRWfixnGXgq2wRq1bH0n3mqDhayiG4LJKgGdDjWXC8W8MMXDYx24SIJrJu9KiNEMprJE+XVF9nQVNijNAjlWBqkDpsfaWTUfeVLRLctfAqW1blsmIv4RQ91PupYJZDNc8nO9ZTF3TEVM+2KHoxzDJrLs2Q==" : null,
            Type = type,
            Status = status
        };
    }

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
