#nullable enable
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Tax.Models;

public class AutomaticTaxFactoryParameters
{
    public AutomaticTaxFactoryParameters(PlanType planType)
    {
        PlanType = planType;
    }

    public AutomaticTaxFactoryParameters(ISubscriber subscriber, IEnumerable<string> prices)
    {
        Subscriber = subscriber;
        Prices = prices;
    }

    public AutomaticTaxFactoryParameters(IEnumerable<string> prices)
    {
        Prices = prices;
    }

    public ISubscriber? Subscriber { get; init; }

    public PlanType? PlanType { get; init; }

    public IEnumerable<string>? Prices { get; init; }
}
