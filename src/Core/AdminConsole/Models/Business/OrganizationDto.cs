using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Business;

public record OrganizationDto
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

    public OrganizationDto()
    {

    }

    public OrganizationDto(Organization organization)
    {
        OrganizationId = organization.Id;
        Seats = organization.Seats;
        MaxAutoScaleSeats = organization.MaxAutoscaleSeats;
        SmSeats = organization.SmSeats;
        SmMaxAutoScaleSeats = organization.MaxAutoscaleSmSeats;
        Plan = StaticStore.GetPlan(organization.PlanType);
        GatewayCustomerId = organization.GatewayCustomerId;
        GatewaySubscriptionId = organization.GatewaySubscriptionId;
        UseSecretsManager = organization.UseSecretsManager;
    }
}
