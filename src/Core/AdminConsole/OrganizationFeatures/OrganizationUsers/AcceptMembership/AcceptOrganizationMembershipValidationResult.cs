namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AcceptMembership;

public record AcceptOrganizationMembershipValidationResult
{
    public bool AutoConfirmPolicyEnabled { get; init; }
}
