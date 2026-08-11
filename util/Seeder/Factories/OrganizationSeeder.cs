using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
namespace Bit.Seeder.Factories;

internal static class OrganizationSeeder
{
    internal static Organization Create(OrganizationSeed seed, IManglerService manglerService)
    {
        var org = new Organization
        {
            Id = CombGuid.Generate(),
            Identifier = manglerService.Mangle(seed.Domain),
            Name = manglerService.Mangle(seed.Name),
            BillingEmail = BillingEmailSeeder.DeriveBillingEmail(seed.Domain),
            Seats = seed.Seats,
            Status = OrganizationStatusType.Created,
            PublicKey = seed.PublicKey,
            PrivateKey = seed.PrivateKey,
            // A fresh Organization has null gateway fields and PlanFeatures never touches them,
            // so direct assignment matches the former "only set non-null values" mutator.
            Gateway = seed.Gateway,
            GatewayCustomerId = seed.GatewayCustomerId,
            GatewaySubscriptionId = seed.GatewaySubscriptionId
        };

        // Order matters: plan defaults first, then overrides layered on top, then Secrets Manager,
        // which reads the PlanType and Seats the earlier steps established.
        PlanFeatures.Apply(org, seed.PlanType);
        PlanFeatures.ApplyOrganizationOverrides(org, seed.Overrides);

        if (seed.EnableSecretsManager)
        {
            PlanFeatures.EnableSecretsManager(org, seed.SmSeats, seed.SmServiceAccounts);
        }

        return org;
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
            Id = CombGuid.Generate(),
            OrganizationId = organization.Id,
            UserId = shouldLinkUserId ? user.Id : null,
            Email = shouldLinkUserId ? null : user.Email,
            Key = shouldIncludeKey ? encryptedOrgKey : null,
            Type = type,
            Status = status
        };
    }
}
