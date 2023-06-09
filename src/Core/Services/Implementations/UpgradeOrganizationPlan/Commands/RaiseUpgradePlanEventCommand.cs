using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.StaticStore;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;


namespace Bit.Core.Services.UpgradeOrganizationPlan.Commands;

public static class RaiseUpgradePlanEventCommand
{
    public static async Task RaiseUpgradePlanEventAsync(Organization organization, Plan existingPlan, Plan newPlan
        , IReferenceEventService referenceEventService, ICurrentContext currentContext)
    {
        await referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.UpgradePlan, organization, currentContext)
            {
                PlanName = newPlan.Name,
                PlanType = newPlan.Type,
                OldPlanName = existingPlan.Name,
                OldPlanType = existingPlan.Type,
                Seats = organization.Seats,
                Storage = organization.MaxStorageGb,
            });
    }
}
