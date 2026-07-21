using Bit.Seeder.Factories;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class SeederBillingTests
{
    [Fact]
    public void DeriveBillingEmail_IsDeterministic_ForSameDomain()
    {
        const string domain = "acme-msp.test";

        var first = SeederBilling.DeriveBillingEmail(domain);
        var second = SeederBilling.DeriveBillingEmail(domain);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveBillingEmail_DiffersByDomain()
    {
        var one = SeederBilling.DeriveBillingEmail("acme-msp.test");
        var two = SeederBilling.DeriveBillingEmail("other-msp.test");

        Assert.NotEqual(one, two);
    }

    [Fact]
    public void DeriveBillingEmail_IsNonDeliverable()
    {
        const string domain = "acme-msp.test";

        var email = SeederBilling.DeriveBillingEmail(domain);

        Assert.StartsWith("billing", email);
        // The domain is nested under a derived hash subdomain, never used as the bare mail domain.
        Assert.EndsWith($".{domain}", email);
        Assert.DoesNotContain($"@{domain}", email);
    }
}
