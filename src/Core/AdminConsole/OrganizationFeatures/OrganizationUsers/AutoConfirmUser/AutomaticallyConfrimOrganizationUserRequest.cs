using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public record AutomaticallyConfirmOrganizationUserRequest
{
    public required Guid OrganizationUserId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string Key { get; init; }

    public required string DefaultUserCollectionName { get; init; }
    public required IActingUser PerformedBy { get; init; }
    public required DateTimeOffset PerformedOn { get; init; }
}

public record AutomaticallyConfirmOrganizationUserRequestData
{
    public required OrganizationUser OrganizationUser { get; init; }

    public required Organization Organization { get; init; }

    public required IActingUser PerformedBy { get; init; }
    public required DateTimeOffset PerformedOn { get; init; }

    public required string Key { get; init; }

    public required string DefaultUserCollectionName { get; init; }

}
