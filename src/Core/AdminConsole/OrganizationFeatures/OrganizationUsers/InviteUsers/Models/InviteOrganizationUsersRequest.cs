using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersRequest(
    OrganizationUserInviteCommandModel[] invites,
    Organization organization,
    Guid performedBy,
    DateTimeOffset performedAt)
{
    public OrganizationUserInviteCommandModel[] Invites { get; } = invites;
    public Organization Organization { get; } = organization;
    public Guid PerformedBy { get; } = performedBy;
    public DateTimeOffset PerformedAt { get; } = performedAt;
}
