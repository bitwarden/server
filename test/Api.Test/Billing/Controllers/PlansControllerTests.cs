using Bit.Api.Billing.Controllers;
using Bit.Core;
using Bit.Core.Billing.Pricing;
using Bit.Core.Models.StaticStore;
using Bit.Core.Test.Billing.Mocks.Plans;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

public class PlansControllerTests
{
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService =
        Substitute.For<Bitwarden.Server.Sdk.Features.IFeatureService>();
    private readonly PlansController _sut;

    public PlansControllerTests()
    {
        _pricingClient.ListPlans().Returns(new List<Plan> { new EnterprisePlan(isAnnual: true), new TeamsPlan(isAnnual: true) });
        _sut = new PlansController(_pricingClient, _featureService);
    }

    [Fact]
    public async Task Get_PamBillingFlagOn_EnterprisePlanCarriesPrivilegedControlsPricing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(true);

        var response = await _sut.Get();

        var enterprise = Assert.Single(response.Data, plan => plan.PrivilegedControls != null);
        Assert.Equal(72, enterprise.PrivilegedControls.SeatPrice);
    }

    [Fact]
    public async Task Get_PamBillingFlagOff_NoPlanCarriesPrivilegedControlsPricing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support).Returns(false);

        var response = await _sut.Get();

        Assert.All(response.Data, plan => Assert.Null(plan.PrivilegedControls));
    }
}
