namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record AcceptInviteLinkMembershipValidationResult
{
    /// <summary>
    /// Whether the Automatic User Confirmation policy is enabled for the organization being joined.
    /// </summary>
    public bool AutoConfirmPolicyEnabled { get; init; }

    /// <summary>
    /// Whether the joining user must be auto-enrolled in account recovery due to the Account Recovery
    /// Administration policy having auto-enrollment enabled.
    /// </summary>
    public bool AutoEnrollEnabled { get; init; }
}
