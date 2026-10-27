using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// The result of <see cref="IJoinOrganizationPolicyValidationService.ValidateAutomaticUserConfirmationPolicyAsync(Guid, Bit.Core.Entities.User, Bit.Core.Enums.OrganizationUserType)"/>.
/// </summary>
/// <param name="Error">Why the user can't join, or <c>null</c> if allowed.</param>
/// <param name="AutoConfirmPolicyEnabled">If true, the caller must remove the user's emergency access once they join.</param>
public sealed record AutomaticUserConfirmationPolicyValidationResult(Error? Error, bool AutoConfirmPolicyEnabled)
{
    /// <summary>True if the user can't join.</summary>
    public bool IsError => Error is not null;
}
