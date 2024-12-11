using Bit.Api.Utilities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class EnumMatchesAttributeTests
{
    [Fact]
    public void IsValid_NullInput_False()
    {
        var enumMatchesAttribute = new EnumMatchesAttribute<PlanType>(
            PlanType.TeamsMonthly,
            PlanType.EnterpriseMonthly
        );

        var result = enumMatchesAttribute.IsValid(null);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_NullAccepted_False()
    {
        var enumMatchesAttribute = new EnumMatchesAttribute<PlanType>();

        var result = enumMatchesAttribute.IsValid(PlanType.TeamsMonthly);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_EmptyAccepted_False()
    {
        var enumMatchesAttribute = new EnumMatchesAttribute<PlanType>([]);

        var result = enumMatchesAttribute.IsValid(PlanType.TeamsMonthly);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_ParseFails_False()
    {
        var enumMatchesAttribute = new EnumMatchesAttribute<PlanType>(
            PlanType.TeamsMonthly,
            PlanType.EnterpriseMonthly
        );

        var result = enumMatchesAttribute.IsValid(GatewayType.Stripe);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_Matches_True()
    {
        var enumMatchesAttribute = new EnumMatchesAttribute<PlanType>(
            PlanType.TeamsMonthly,
            PlanType.EnterpriseMonthly
        );

        var result = enumMatchesAttribute.IsValid(PlanType.TeamsMonthly);

        Assert.True(result);
    }
}
