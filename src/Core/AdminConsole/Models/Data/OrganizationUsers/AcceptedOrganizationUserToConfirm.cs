namespace Bit.Core.AdminConsole.Models.Data.OrganizationUsers;

public record AcceptedOrganizationUserToConfirm
{
    public required Guid OrganizationUserId { get; init; }
    public required Guid UserId { get; init; }
    public required string Key { get; init; }
}
