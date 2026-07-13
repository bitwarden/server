using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record AcceptOrganizationInviteLinkRequest
{
    public required Guid OrganizationId { get; init; }
    public required Guid Code { get; init; }
    public required User User { get; init; }
    public string? ResetPasswordKey { get; init; }
}
