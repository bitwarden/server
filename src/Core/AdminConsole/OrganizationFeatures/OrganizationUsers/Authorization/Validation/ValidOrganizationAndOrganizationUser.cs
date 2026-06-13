using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.Validation;

public record ValidOrganizationAndOrganizationUser
{
    public Organization? Organization { get; init; }
    public OrganizationUser? OrganizationUser { get; init; }

    public static ValidOrganizationAndOrganizationUser Empty => new();
}
