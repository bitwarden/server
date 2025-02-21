using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

public class InviteUserOrganizationValidationRequest
{
    public OrganizationUserInviteDto[] Invites { get; init; } = [];
    public OrganizationDto Organization { get; init; }
    public Guid PerformedBy { get; init; }
    public DateTimeOffset PerformedAt { get; init; }
    public int OccupiedPmSeats { get; init; }
    public int OccupiedSmSeats { get; init; }
    public PasswordManagerSubscriptionUpdate PasswordManagerSubscriptionUpdate { get; set; }
    public SecretsManagerSubscriptionUpdate SecretsManagerSubscriptionUpdate { get; set; }
}
