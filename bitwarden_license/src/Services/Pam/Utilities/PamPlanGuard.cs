using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Services.Pam.Utilities;

/// <summary>
/// The organization plan check (<c>Organization.UsePam</c>) on the access connector paths that grant access.
/// </summary>
/// <remarks>
/// Guards register, enable and assign only; disable, delete, unassign and reads stay open, so an admin can still
/// clean up after the organization loses PAM.
/// </remarks>
public static class PamPlanGuard
{
    public const string NoPamPlanMessage = "Your organization's plan does not include Privileged Controls.";

    /// <summary>
    /// Throws <see cref="BadRequestException"/> with <see cref="NoPamPlanMessage"/> if the organization is missing
    /// or its plan does not include PAM.
    /// </summary>
    public static async Task RequirePamPlanAsync(
        this IOrganizationRepository organizationRepository, Guid organizationId)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is not { UsePam: true })
        {
            throw new BadRequestException(NoPamPlanMessage);
        }
    }
}
