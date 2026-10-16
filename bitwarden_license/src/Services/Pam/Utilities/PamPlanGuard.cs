using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Exceptions;

namespace Bit.Services.Pam.Utilities;

/// <summary>
/// The organization plan check (<c>OrganizationAbility.UsePam</c>) on the access connector paths that grant access.
/// </summary>
/// <remarks>
/// Guards register, enable and assign only; disable, delete, unassign and reads stay open, so an admin can still
/// clean up after the organization loses PAM.
/// Reads the organization ability cache, which plan changes update; a direct database edit applies when the cache
/// entry expires.
/// </remarks>
public static class PamPlanGuard
{
    public const string NoPamPlanMessage = "Your organization's plan does not include Privileged Controls.";

    /// <summary>
    /// Throws <see cref="BadRequestException"/> with <see cref="NoPamPlanMessage"/> if the organization is missing
    /// or its plan does not include PAM.
    /// </summary>
    public static async Task RequirePamPlanAsync(
        this IOrganizationAbilityCacheService organizationAbilityCacheService, Guid organizationId)
    {
        var ability = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (ability is not { UsePam: true })
        {
            throw new BadRequestException(NoPamPlanMessage);
        }
    }
}
