// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteOrganizationUsersValidationRequest
{
    public InviteOrganizationUsersValidationRequest()
    {
    }

    public InviteOrganizationUsersValidationRequest(InviteOrganizationUsersValidationRequest request)
    {
        Invites = request.Invites;
        InviteOrganization = request.InviteOrganization;
        PerformedBy = request.PerformedBy;
        PerformedAt = request.PerformedAt;
        OccupiedPmSeats = request.OccupiedPmSeats;
        OccupiedSmSeats = request.OccupiedSmSeats;
    }

    public InviteOrganizationUsersValidationRequest(InviteOrganizationUsersValidationRequest request,
        PasswordManagerSubscriptionUpdate subscriptionUpdate,
        SecretsManagerSubscriptionUpdate smSubscriptionUpdate)
        : this(request)
    {
        PasswordManagerSubscriptionUpdate = subscriptionUpdate;
        SecretsManagerSubscriptionUpdate = smSubscriptionUpdate;
    }

    public OrganizationUserInviteCommandModel[] Invites { get; init; } = [];
    public InviteOrganization InviteOrganization { get; init; }
    public Guid PerformedBy { get; init; }
    public DateTimeOffset PerformedAt { get; init; }
    public int OccupiedPmSeats { get; init; }
    public int OccupiedSmSeats { get; init; }
    public PasswordManagerSubscriptionUpdate PasswordManagerSubscriptionUpdate { get; set; }
    public SecretsManagerSubscriptionUpdate SecretsManagerSubscriptionUpdate { get; set; }
}
