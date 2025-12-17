using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.RustSDK;

namespace Bit.Seeder.Factories;

public class OrganizationSeeder
{
    public static Organization CreateEnterprise(string name, string domain, int seats, OrganizationKeys orgKeys)
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
            Enabled = true,  // Required for OrganizationAbility cache
            //GatewayCustomerId = "example-customer-id",
            //GatewaySubscriptionId = "example-subscription-id",
            MaxStorageGb = 10,
            PublicKey = orgKeys.PublicKey,
            PrivateKey = orgKeys.PrivateKey,
        };
    }
}

public static class OrgnaizationExtensions
{
    /// <summary>
    /// Creates an OrganizationUser with fields populated based on status.
    /// For Invited status, only user.Email is used. For other statuses, user.Id is used.
    /// </summary>
    public static OrganizationUser CreateOrganizationUser(
        this Organization organization, User user, OrganizationUserType type, OrganizationUserStatusType status,
        string? organizationKey = null, RustSdkService? rustSdkService = null)
    {
        var isInvited = status == OrganizationUserStatusType.Invited;
        var isConfirmed = status == OrganizationUserStatusType.Confirmed || status == OrganizationUserStatusType.Revoked;

        string? encryptedOrgKey = null;
        if (isConfirmed && organizationKey != null && rustSdkService != null && user.PublicKey != null)
        {
            // Dynamically generate the encrypted organization key for this user
            encryptedOrgKey = rustSdkService.GenerateUserOrganizationKey(user.PublicKey, organizationKey);
        }
        else if (isConfirmed)
        {
            // Fallback to hardcoded key for backward compatibility
            encryptedOrgKey = "4.rY01mZFXHOsBAg5Fq4gyXuklWfm6mQASm42DJpx05a+e2mmp+P5W6r54WU2hlREX0uoTxyP91bKKwickSPdCQQ58J45LXHdr9t2uzOYyjVzpzebFcdMw1eElR9W2DW8wEk9+mvtWvKwu7yTebzND+46y1nRMoFydi5zPVLSlJEf81qZZ4Uh1UUMLwXz+NRWfixnGXgq2wRq1bH0n3mqDhayiG4LJKgGdDjWXC8W8MMXDYx24SIJrJu9KiNEMprJE+XVF9nQVNijNAjlWBqkDpsfaWTUfeVLRLctfAqW1blsmIv4RQ91PupYJZDNc8nO9ZTF3TEVM+2KHoxzDJrLs2Q==";
        }

        return new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = isInvited ? null : user.Id,
            Email = isInvited ? user.Email : null,
            Key = encryptedOrgKey,
            Type = type,
            Status = status
        };
    }

    public static OrganizationUser CreateSdkOrganizationUser(this Organization organization, User user,
        string organizationKey, RustSdkService rustSdkService)
    {
        if (user.PublicKey == null)
        {
            throw new ArgumentException("User must have a PublicKey to create an SDK organization user", nameof(user));
        }

        // Dynamically generate the encrypted organization key for this user
        var encryptedOrgKey = rustSdkService.GenerateUserOrganizationKey(user.PublicKey, organizationKey);
        Console.WriteLine($"Generated encrypted organization key for SDK user {user.Email}");

        return new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = user.Id,
            Key = encryptedOrgKey,
            Type = OrganizationUserType.Admin,
            Status = OrganizationUserStatusType.Confirmed
        };
    }
}
