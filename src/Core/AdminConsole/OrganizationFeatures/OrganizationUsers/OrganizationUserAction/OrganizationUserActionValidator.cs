using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// Validates that an acting user is permitted to manage a target member without escalating privileges,
/// according to Bitwarden's organization role hierarchy:
/// <list type="bullet">
/// <item>Owners (and provider users) can manage Owners, Admins, or Users.</item>
/// <item>Admins can manage Admins or Users.</item>
/// <item>Custom users can manage Users and other Custom users, but only when they hold the manage permission required for the action.</item>
/// <item>All other members (including regular Users) cannot manage any member.</item>
/// </list>
/// A role can only be raised to a level the acting user already holds (e.g. only an Owner can grant the
/// Owner role).
/// </summary>
public static class OrganizationUserActionValidator
{
    /// <summary>
    /// Validates whether the acting user can manage members at all, independent of any target. Use this where
    /// authorization needs the gate without a specific target. Returns valid, or a <see cref="MissingManagePermissionError"/>.
    /// </summary>
    public static async Task<ValidationResult<OrganizationUserManageMembersRequest>> ValidateCanManageMembersAsync(OrganizationUserManageMembersRequest request)
    {
        var isProvider = !IsAuthorizedByRole(request) && await request.IsProviderUserForOrg();

        return IsAuthorizedByRole(request) || isProvider
            ? Valid(request)
            : Invalid(request, new MissingManagePermissionError());
    }

    /// <summary>
    /// Validates whether the acting user may manage (or assign) the target role: the management-authority gate
    /// first, then role escalation. Returns valid, a <see cref="MissingManagePermissionError"/> if they cannot
    /// manage members at all, or a <see cref="CannotManageHigherRoleError"/> if the target role outranks their authority.
    /// </summary>
    /// <remarks>
    /// Covers management authority and role escalation only. Callers remain responsible for other checks such as
    /// self-actions, system/automated users, and confirmed-owner counts.
    /// </remarks>
    public static async Task<ValidationResult<OrganizationUserActionRequest>> ValidateAsync(OrganizationUserActionRequest request)
    {
        // First, the management-authority gate (shared with authorization).
        var canManageResult = await ValidateCanManageMembersAsync(request);
        if (canManageResult.IsError)
        {
            return Invalid(request, canManageResult.AsError);
        }

        // The acting user can manage members; confirm the target's role is within their authority. A provider
        // user has Owner-level authority, so treat them as an Owner. The expensive provider lookup already ran
        // inside the gate above, so we reuse the cheap synchronous role check here rather than repeating it.
        var effectiveRole = IsAuthorizedByRole(request) ? request.ActingUserRole : OrganizationUserType.Owner;

        return request.TargetUserRole switch
        {
            OrganizationUserType.Owner when effectiveRole is OrganizationUserType.Owner => Valid(request),
            OrganizationUserType.Admin when effectiveRole is OrganizationUserType.Owner or OrganizationUserType.Admin => Valid(request),
            OrganizationUserType.User or OrganizationUserType.Custom
                when effectiveRole is OrganizationUserType.Owner or OrganizationUserType.Admin or OrganizationUserType.Custom => Valid(request),
            _ => Invalid(request, new CannotManageHigherRoleError())
        };
    }

    /// <summary>
    /// Whether the acting user can manage members by their organization role alone, without the provider lookup:
    /// Owners and Admins always, and Custom users holding the action's permission. Cheap and synchronous, so it
    /// is safe to evaluate more than once.
    /// </summary>
    private static bool IsAuthorizedByRole(OrganizationUserManageMembersRequest request) =>
        request.ActingUserRole switch
        {
            OrganizationUserType.Owner => true,
            OrganizationUserType.Admin => true,
            OrganizationUserType.Custom when request.ActingUserPermissions != null
                && request.PermissionPicker(request.ActingUserPermissions) => true,
            _ => false
        };
}
