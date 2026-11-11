using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Subscriptions.Models;

public record OrganizationRestart(Plan Plan, IReadOnlyList<OrganizationRestartItem> Items);

public record OrganizationRestartItem(string PriceId, long Quantity);
