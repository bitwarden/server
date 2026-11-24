using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;

#nullable enable

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests that need a user to belong to an organization.
/// </summary>
public static class OrganizationTestHelpers
{
    /// <summary>
    /// Creates a new Enterprise organization and makes the user a confirmed member of it.
    /// </summary>
    public static async Task<OrganizationUser> CreateOrganizationWithConfirmedMemberAsync(
        IdentityApplicationFactory factory,
        Guid userId,
        OrganizationUserType organizationUserType,
        Permissions? permissions = null,
        bool accessSecretsManager = false,
        bool accessPam = false)
    {
        var organizationId = Guid.NewGuid();
        await factory.Services.GetRequiredService<IOrganizationRepository>().CreateAsync(new Organization
        {
            Id = organizationId,
            Name = $"Org Name | {organizationId}",
            Enabled = true,
            Plan = "Enterprise",
            UsePolicies = true,
            BillingEmail = $"billing-email+{organizationId}@example.com",
            UseSecretsManager = accessSecretsManager,
            UsePam = accessPam,
        });

        var organizationUser = new OrganizationUser
        {
            OrganizationId = organizationId,
            UserId = userId,
            Status = OrganizationUserStatusType.Confirmed,
            Type = organizationUserType,
            AccessSecretsManager = accessSecretsManager,
            AccessPam = accessPam,
        };
        if (permissions != null)
        {
            organizationUser.SetPermissions(permissions);
        }

        return await factory.Services.GetRequiredService<IOrganizationUserRepository>().CreateAsync(organizationUser);
    }
}
