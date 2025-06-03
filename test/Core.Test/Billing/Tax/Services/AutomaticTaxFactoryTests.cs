using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Billing.Tax.Services.Implementations;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Tax.Services;

[SutProviderCustomize]
public class AutomaticTaxFactoryTests
{
    [BitAutoData]
    [Theory]
    public async Task CreateAsync_ReturnsPersonalUseStrategy_WhenSubscriberIsUser(SutProvider<AutomaticTaxFactory> sut)
    {
        var parameters = new AutomaticTaxFactoryParameters(new User(), []);

        var actual = await sut.Sut.CreateAsync(parameters);

        Assert.IsType<PersonalUseAutomaticTaxStrategy>(actual);
    }

    [BitAutoData]
    [Theory]
    public async Task CreateAsync_ReturnsPersonalUseStrategy_WhenSubscriberIsOrganizationWithFamiliesAnnuallyPrice(
        SutProvider<AutomaticTaxFactory> sut)
    {
        var familiesPlan = new FamiliesPlan();
        var parameters = new AutomaticTaxFactoryParameters(new Organization(), [familiesPlan.PasswordManager.StripePlanId]);

        sut.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(new FamiliesPlan());

        sut.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually2019))
            .Returns(new Families2019Plan());

        var actual = await sut.Sut.CreateAsync(parameters);

        Assert.IsType<PersonalUseAutomaticTaxStrategy>(actual);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_ReturnsBusinessUseStrategy_WhenSubscriberIsOrganizationWithBusinessUsePrice(
        EnterpriseAnnually plan,
        SutProvider<AutomaticTaxFactory> sut)
    {
        var parameters = new AutomaticTaxFactoryParameters(new Organization(), [plan.PasswordManager.StripePlanId]);

        sut.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually))
            .Returns(new FamiliesPlan());

        sut.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == PlanType.FamiliesAnnually2019))
            .Returns(new Families2019Plan());

        var actual = await sut.Sut.CreateAsync(parameters);

        Assert.IsType<BusinessUseAutomaticTaxStrategy>(actual);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_ReturnsPersonalUseStrategy_WhenPlanIsMeantForPersonalUse(SutProvider<AutomaticTaxFactory> sut)
    {
        var parameters = new AutomaticTaxFactoryParameters(PlanType.FamiliesAnnually);
        sut.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == parameters.PlanType.Value))
            .Returns(new FamiliesPlan());

        var actual = await sut.Sut.CreateAsync(parameters);

        Assert.IsType<PersonalUseAutomaticTaxStrategy>(actual);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_ReturnsBusinessUseStrategy_WhenPlanIsMeantForBusinessUse(SutProvider<AutomaticTaxFactory> sut)
    {
        var parameters = new AutomaticTaxFactoryParameters(PlanType.EnterpriseAnnually);
        sut.GetDependency<IPricingClient>()
            .GetPlanOrThrow(Arg.Is<PlanType>(p => p == parameters.PlanType.Value))
            .Returns(new EnterprisePlan(true));

        var actual = await sut.Sut.CreateAsync(parameters);

        Assert.IsType<BusinessUseAutomaticTaxStrategy>(actual);
    }

    public record EnterpriseAnnually : EnterprisePlan
    {
        public EnterpriseAnnually() : base(true)
        {
        }
    }
}
