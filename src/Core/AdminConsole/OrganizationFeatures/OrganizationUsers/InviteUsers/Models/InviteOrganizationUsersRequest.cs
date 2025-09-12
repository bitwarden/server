using System.Data.Common;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersRequest
{
    public OrganizationUserInviteCommandModel[] Invites { get; } = [];
    public InviteOrganization InviteOrganization { get; }
    public Guid PerformedBy { get; }
    public DateTimeOffset PerformedAt { get; }
    public DbTransaction? Transaction { get; set; }

    public InviteOrganizationUsersRequest(OrganizationUserInviteCommandModel[] invites,
        InviteOrganization inviteOrganization,
        Guid performedBy,
        DateTimeOffset performedAt)
    {
        Invites = invites;
        InviteOrganization = inviteOrganization;
        PerformedBy = performedBy;
        PerformedAt = performedAt;
    }

    public InviteOrganizationUsersRequest(OrganizationUserInviteCommandModel[] invites,
        InviteOrganization inviteOrganization,
        Guid performedBy,
        DateTimeOffset performedAt,
        DbTransaction transaction) : this(invites, inviteOrganization, performedBy, performedAt)
    {
        Transaction = transaction;
    }
}
