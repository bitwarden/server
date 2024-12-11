using AutoFixture;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture;

public class SubscriptionInfoCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SubscriptionInfoCustomization();
}

public class SubscriptionInfoCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        // The Subscription property uses the external Stripe library, which Autofixture doesn't handle
        fixture.Customize<SubscriptionInfo>(c => c.Without(s => s.Subscription));
    }
}
