using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

/// <summary>
/// Policy checks for a user joining an organization.
/// </summary>
/// <remarks>
/// The user's other organizations are checked via <see cref="IPolicyRequirementQuery"/>. The organization being joined
/// is checked via <see cref="IPreAccessPolicyEnforcer"/>, since the requirement framework doesn't cover new or Staged
/// members. Each check has an overload that loads its own data and one that takes preloaded data, for running several
/// checks at once.
/// </remarks>
public interface IJoinOrganizationPolicyValidationService
{
    /// <summary>
    /// Checks the Single Organization policy, both for the user's other organizations and the organization being joined.
    /// </summary>
    /// <param name="organizationId">The organization being joined.</param>
    /// <param name="user">The user joining.</param>
    /// <param name="proposedRole">The user's existing role, or <see cref="OrganizationUserType.User"/> if new.</param>
    /// <returns><c>null</c> if allowed, otherwise the error.</returns>
    /// <exception cref="PreAccessOrganizationNotFoundException">The organization does not exist.</exception>
    Task<Error?> ValidateSingleOrganizationPolicyAsync(Guid organizationId, User user, OrganizationUserType proposedRole);

    /// <inheritdoc cref="ValidateSingleOrganizationPolicyAsync(Guid, User, OrganizationUserType)" path="/summary|/param|/returns"/>
    /// <param name="policyEnforcer">The policy enforcer for the organization being joined.</param>
    /// <param name="allOrganizationMemberships">All of the user's organization memberships.</param>
    Task<Error?> ValidateSingleOrganizationPolicyAsync(Guid organizationId, User user, OrganizationUserType proposedRole,
        IPreAccessPolicyEnforcer policyEnforcer, ICollection<OrganizationUser> allOrganizationMemberships);

    /// <summary>
    /// Checks the Require Two-Factor Authentication policy of the organization being joined.
    /// </summary>
    /// <inheritdoc cref="ValidateSingleOrganizationPolicyAsync(Guid, User, OrganizationUserType)" path="/param|/returns|/exception"/>
    Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(Guid organizationId, User user,
        OrganizationUserType proposedRole);

    /// <inheritdoc cref="ValidateTwoFactorAuthenticationPolicyAsync(Guid, User, OrganizationUserType)" path="/summary|/param|/returns"/>
    /// <param name="policyEnforcer">The policy enforcer for the organization being joined.</param>
    Task<Error?> ValidateTwoFactorAuthenticationPolicyAsync(Guid organizationId, User user,
        OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer);

    /// <summary>
    /// Checks the Automatic User Confirmation policy, both for the user's other organizations and the organization
    /// being joined.
    /// </summary>
    /// <inheritdoc cref="ValidateSingleOrganizationPolicyAsync(Guid, User, OrganizationUserType)" path="/param|/exception"/>
    Task<AutomaticUserConfirmationPolicyValidationResult> ValidateAutomaticUserConfirmationPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole);

    /// <inheritdoc cref="ValidateAutomaticUserConfirmationPolicyAsync(Guid, User, OrganizationUserType)" path="/summary|/param"/>
    /// <param name="policyEnforcer">The policy enforcer for the organization being joined.</param>
    /// <param name="allOrganizationMemberships">All of the user's organization memberships.</param>
    Task<AutomaticUserConfirmationPolicyValidationResult> ValidateAutomaticUserConfirmationPolicyAsync(
        Guid organizationId, User user, OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer,
        ICollection<OrganizationUser> allOrganizationMemberships);

    /// <summary>
    /// Checks the Account Recovery policy of the organization being joined.
    /// </summary>
    /// <inheritdoc cref="ValidateSingleOrganizationPolicyAsync(Guid, User, OrganizationUserType)" path="/param|/exception"/>
    /// <param name="resetPasswordKey">The user's reset password key, if any.</param>
    Task<AccountRecoveryPolicyValidationResult> ValidateAccountRecoveryPolicyAsync(Guid organizationId, User user,
        OrganizationUserType proposedRole, string? resetPasswordKey);

    /// <inheritdoc cref="ValidateAccountRecoveryPolicyAsync(Guid, User, OrganizationUserType, string?)" path="/summary|/param"/>
    /// <param name="policyEnforcer">The policy enforcer for the organization being joined.</param>
    AccountRecoveryPolicyValidationResult ValidateAccountRecoveryPolicy(Guid organizationId, User user,
        OrganizationUserType proposedRole, IPreAccessPolicyEnforcer policyEnforcer, string? resetPasswordKey);
}
