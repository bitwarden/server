using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public record UserNotCompliantWithSingleOrganization() : BadRequestError("All members must be compliant with the single organization membership policy before you can enable the automatically confirm invited users policy. Remove members who belong to multiple organization vaults.");

public record ProviderExistsInOrganization() : BadRequestError("This organization has members with the Provider user type. Remove those members before enabling automatically confirm invited users.");
