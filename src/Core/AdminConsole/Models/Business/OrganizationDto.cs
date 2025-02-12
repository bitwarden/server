using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Business;

public record OrganizationDto(
    Guid OrganizationId,
    bool UseCustomPermissions,
    int? Seats,
    int? MaxAutoScaleSeats,
    int? SmSeats,
    int? SmMaxAutoScaleSeats,
    Plan Plan,
    string GatewayCustomerId,
    string GatewaySubscriptionId,
    bool UseSecretsManager
)
{
    public static OrganizationDto FromOrganization(Organization organization) =>
        new(organization.Id,
            organization.UseCustomPermissions,
            organization.Seats,
            organization.MaxAutoscaleSeats,
            organization.SmSeats,
            organization.MaxAutoscaleSmSeats,
            StaticStore.GetPlan(organization.PlanType),
            organization.GatewayCustomerId,
            organization.GatewaySubscriptionId,
            organization.UseSecretsManager);
};
