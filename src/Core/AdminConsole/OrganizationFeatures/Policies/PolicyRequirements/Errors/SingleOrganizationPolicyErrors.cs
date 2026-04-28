using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;

public record UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy()
    : BadRequestError("Member cannot join the organization because they are in another organization which forbids it.");

public record UserIsAMemberOfAnotherOrganization()
    : BadRequestError("Member cannot join the organization until they leave or remove all other organizations.");

public record UserCannotCreateOrg()
    : BadRequestError("You may not create an organization. You belong to an organization " +
                      "which has a policy that prohibits you from being a member of any other organization.");
