using Bit.Core.Billing.Enums;
using Bit.Core.Providers.Entities;

namespace Bit.Core.Billing.Providers.Models;

public record ChangeProviderPlanCommand(
    Provider Provider,
    Guid ProviderPlanId,
    PlanType NewPlan);
