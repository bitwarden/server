using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// The result of <see cref="IJoinOrganizationPolicyValidationService.ValidateAccountRecoveryPolicyAsync(Guid, Bit.Core.Entities.User, Bit.Core.Enums.OrganizationUserType, string?)"/>.
/// </summary>
/// <param name="Error">Why the user can't join, or <c>null</c> if allowed.</param>
/// <param name="AutoEnrollEnabled">If true, the caller must enroll the user in account recovery once they join.</param>
public sealed record AccountRecoveryPolicyValidationResult(Error? Error, bool AutoEnrollEnabled)
{
    /// <summary>True if the user can't join.</summary>
    public bool IsError => Error is not null;
}
