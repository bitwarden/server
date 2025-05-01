using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Pricing.Enums;

namespace Bit.Core.Billing.Providers.Models;

public record ChangeProviderPlanCommand(
    Provider Provider,
    Guid ProviderPlanId,
    PlanType NewPlan);
