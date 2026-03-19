using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Models.Business;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Models;

public class OrganizationSaleTests
{
    [Fact]
    public void From_WithUserCoupons_PopulatesCustomerSetupCoupons()
    {
        var organization = new Organization();
        var signup = new OrganizationSignup
        {
            IsFromProvider = false,
            IsFromSecretsManagerTrial = false,
            Coupons = new[] { "COUPON_ONE", "COUPON_TWO" }
        };

        var sale = OrganizationSale.From(organization, signup);

        Assert.NotNull(sale.CustomerSetup);
        Assert.Equal(new[] { "COUPON_ONE", "COUPON_TWO" }, sale.CustomerSetup.Coupons);
    }

    [Fact]
    public void From_WithNoCoupons_CustomerSetupCouponsIsNull()
    {
        var organization = new Organization();
        var signup = new OrganizationSignup
        {
            IsFromProvider = false,
            IsFromSecretsManagerTrial = false,
            Coupons = null
        };

        var sale = OrganizationSale.From(organization, signup);

        Assert.NotNull(sale.CustomerSetup);
        Assert.Null(sale.CustomerSetup.Coupons);
    }

    [Fact]
    public void From_WithProviderSignup_UsesMSPCouponAndIgnoresUserCoupons()
    {
        var organization = new Organization();
        var signup = new OrganizationSignup
        {
            IsFromProvider = true,
            Coupons = ["USER_COUPON"]
        };

        var sale = OrganizationSale.From(organization, signup);

        Assert.NotNull(sale.CustomerSetup);
        Assert.Equal(new[] { StripeConstants.CouponIDs.LegacyMSPDiscount }, sale.CustomerSetup.Coupons);
    }

    [Fact]
    public void From_WithSMTrialSignup_UsesSMCouponAndIgnoresUserCoupons()
    {
        var organization = new Organization();
        var signup = new OrganizationSignup
        {
            IsFromProvider = false,
            IsFromSecretsManagerTrial = true,
            Coupons = ["USER_COUPON"]
        };

        var sale = OrganizationSale.From(organization, signup);

        Assert.NotNull(sale.CustomerSetup);
        Assert.Equal(new[] { StripeConstants.CouponIDs.SecretsManagerStandalone }, sale.CustomerSetup.Coupons);
    }
}
