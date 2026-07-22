using Bit.Seeder.Factories;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class BillingEmailSeederTests
{
    [Fact]
    public void DeriveBillingEmail_IsDeterministic_ForSameDomain()
    {
        const string domain = "acme-msp.test";

        var first = BillingEmailSeeder.DeriveBillingEmail(domain);
        var second = BillingEmailSeeder.DeriveBillingEmail(domain);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveBillingEmail_DiffersByDomain()
    {
        var one = BillingEmailSeeder.DeriveBillingEmail("acme-msp.test");
        var two = BillingEmailSeeder.DeriveBillingEmail("other-msp.test");

        Assert.NotEqual(one, two);
    }

    [Fact]
    public void DeriveBillingEmail_IsNonDeliverable()
    {
        const string domain = "acme-msp.test";

        var email = BillingEmailSeeder.DeriveBillingEmail(domain);

        Assert.StartsWith("billing", email);
        // The domain is nested under a derived hash subdomain, never used as the bare mail domain.
        Assert.EndsWith($".{domain}", email);
        Assert.DoesNotContain($"@{domain}", email);
    }
}
