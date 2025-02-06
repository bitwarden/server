using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    string GatewaySubscriptionId
) : ISubscriber
{
    public Guid Id => OrganizationId;
    public GatewayType? Gateway { get; set; }
    public string GatewayCustomerId { get; set; } = GatewayCustomerId;
    public string GatewaySubscriptionId { get; set; } = GatewaySubscriptionId;
    public string BillingEmailAddress() => throw new NotImplementedException();

    public string BillingName() => throw new NotImplementedException();

    public string SubscriberName() => throw new NotImplementedException();

    public string BraintreeCustomerIdPrefix() => throw new NotImplementedException();

    public string BraintreeIdField() => throw new NotImplementedException();

    public string BraintreeCloudRegionField() => throw new NotImplementedException();

    public bool IsOrganization() => throw new NotImplementedException();

    public bool IsUser() => throw new NotImplementedException();

    public string SubscriberType() => throw new NotImplementedException();

    public bool IsExpired() => throw new NotImplementedException();

    public static OrganizationDto FromOrganization(Organization organization) =>
        new(organization.Id,
            organization.UseCustomPermissions,
            organization.Seats,
            organization.MaxAutoscaleSeats,
            organization.SmSeats,
            organization.MaxAutoscaleSmSeats,
            StaticStore.GetPlan(organization.PlanType),
            organization.GatewayCustomerId,
            organization.GatewaySubscriptionId);
}
