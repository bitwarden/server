// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.Models.Business;

public record InviteOrganization
{
    public Guid OrganizationId { get; init; }
    public int? Seats { get; init; }
    public int? MaxAutoScaleSeats { get; init; }
    public int? SmSeats { get; init; }
    public int? SmMaxAutoScaleSeats { get; init; }
    public Plan Plan { get; init; }
    public string GatewayCustomerId { get; init; }
    public string GatewaySubscriptionId { get; init; }
    public bool UseSecretsManager { get; init; }

    public InviteOrganization()
    {

    }

    public InviteOrganization(Organization organization, Plan plan)
    {
        OrganizationId = organization.Id;
        Seats = organization.Seats;
        MaxAutoScaleSeats = organization.MaxAutoscaleSeats;
        SmSeats = organization.SmSeats;
        SmMaxAutoScaleSeats = organization.MaxAutoscaleSmSeats;
        Plan = plan;
        GatewayCustomerId = organization.GatewayCustomerId;
        GatewaySubscriptionId = organization.GatewaySubscriptionId;
        UseSecretsManager = organization.UseSecretsManager;
    }
}
