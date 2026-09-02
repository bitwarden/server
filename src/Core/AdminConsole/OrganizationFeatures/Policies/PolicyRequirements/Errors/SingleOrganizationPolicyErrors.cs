using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;

public record UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy()
    : BadRequestError("Member cannot join this organization's vault because they are a member of another organization which forbids it.");

public record UserIsAMemberOfAnotherOrganization()
    : BadRequestError("Member cannot join this organization vault until they leave all other organization vaults.");

public record UserCannotCreateOrg()
    : BadRequestError("You cannot create a new organization vault because your current membership doesn't allow you to join or create additional organization vaults.");

public record UserCannotAcceptInviteMemberOfAnotherOrg()
    : BadRequestError("You cannot accept this invite until you leave all other organization vaults.");

public record UserCannotAcceptInviteForbiddenByOtherOrg()
    : BadRequestError("You cannot accept this invite because you are a member of another organization vault which forbids it.");

public record UserCannotBeConfirmedMemberOfAnotherOrg(string Email)
    : BadRequestError($"{Email} cannot be confirmed until they leave all other organization vaults.");

public record UserCannotBeConfirmedForbiddenByOtherOrg(string Email)
    : BadRequestError($"{Email} cannot be confirmed because they are a member of another organization which forbids it.");

public record UserCannotBeRestoredMemberOfAnotherOrg(string Email)
    : BadRequestError($"{Email} cannot be restored until they leave all other organization vaults.");

public record UserCannotBeRestoredForbiddenByOtherOrg(string Email)
    : BadRequestError($"{Email} cannot be restored because they are a member of another organization which forbids it.");
