using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public record UserNotCompliantWithSingleOrganization() : BadRequestError("All organization users must be compliant with the Single organization policy before enabling the Automatically confirm invited users policy. Please remove users who are members of multiple organizations.");

public record ProviderExistsInOrganization() : BadRequestError("The organization has users with the Provider user type. Please remove provider users before enabling the Automatically confirm invited users policy.");
