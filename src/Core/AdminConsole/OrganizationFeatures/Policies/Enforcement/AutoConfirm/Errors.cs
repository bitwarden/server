using Bit.Core.AdminConsole.Utilities.v2;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

public record AutoConfirmDoesNotAllowMembershipToOtherOrganizations() : BadRequestError("Automatic User Confirmation policy does not support membership to other organizations.");
