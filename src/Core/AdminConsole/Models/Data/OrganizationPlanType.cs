using Bit.Core.Billing.Enums;

namespace Bit.Core.AdminConsole.Models.Data;

/// <summary>Lightweight projection of <c>(Organization.Id, Organization.PlanType)</c>.</summary>
public class OrganizationPlanType
{
    public Guid OrganizationId { get; set; }
    public PlanType PlanType { get; set; }
}
