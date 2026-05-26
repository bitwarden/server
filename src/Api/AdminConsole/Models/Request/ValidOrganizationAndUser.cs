using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Api.AdminConsole.Models.Request;

public record ValidOrganizationAndUser
{
    public required Organization Organization { get; init; }
    public required OrganizationUser OrganizationUser { get; init; }
}
