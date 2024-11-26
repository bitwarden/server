using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUpCommand;

public record SignUpOrganizationResponse(
    Organization Organization,
    OrganizationUser OrganizationUser,
    Collection DefaultCollection) : CommandResult;
