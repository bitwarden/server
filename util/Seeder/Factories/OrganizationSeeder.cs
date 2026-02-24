using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Services;

namespace Bit.Seeder.Factories;

internal static class OrganizationSeeder
{
    internal static Organization Create(string name, string domain, int seats, IManglerService manglerService, string? publicKey = null, string? privateKey = null, PlanType planType = PlanType.EnterpriseAnnually)
    {
        var org = new Organization
        {
            Id = CoreHelpers.GenerateComb(),
            Identifier = manglerService.Mangle(domain),
            Name = manglerService.Mangle(name),
            BillingEmail = $"billing@{domain}",
            Seats = seats,
            Status = OrganizationStatusType.Created,
            PublicKey = publicKey,
            PrivateKey = privateKey
        };

        PlanFeatures.Apply(org, planType);

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
