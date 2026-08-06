using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Organizations.Models;

/// <summary>Lightweight projection of <c>(Organization.Id, Organization.PlanType)</c>.</summary>
public class OrganizationPlanType
{
    public Guid OrganizationId { get; set; }
    public PlanType PlanType { get; set; }
}
