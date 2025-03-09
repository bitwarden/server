using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Services.Contracts;

public record ChangeProviderPlanCommand(
    Guid ProviderPlanId,
    PlanType NewPlan,
    string GatewaySubscriptionId);
