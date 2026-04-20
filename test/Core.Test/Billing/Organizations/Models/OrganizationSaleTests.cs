using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Models.Business;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Models;

public class OrganizationSaleTests
{
    [Fact]
    public void From_WithProviderSignup_UsesMSPCouponInSystemCoupons()
    {
        var organization = new Organization();
        var signup = new OrganizationSignup
        {
            IsFromProvider = true,
            Coupons = ["USER_COUPON"]
        };

        var sale = OrganizationSale.From(organization, signup);

        Assert.NotNull(sale.CustomerSetup);
        Assert.Equal(new[] { StripeConstants.CouponIDs.LegacyMSPDiscount }, sale.CustomerSetup.SystemCoupons);
        Assert.Null(sale.CustomerSetup.DiscountCoupons);
    }

    [Fact]
    public void From_WithSMTrialSignup_UsesSMCouponInSystemCoupons()
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
        Assert.Equal(new[] { StripeConstants.CouponIDs.SecretsManagerStandalone }, sale.CustomerSetup.SystemCoupons);
        Assert.Null(sale.CustomerSetup.DiscountCoupons);
    }

    [Fact]
    public void From_WithUserCoupons_PopulatesCustomerSetupDiscountCoupons()
    {
        var organization = new Organization();
        var signup = new OrganizationSignup
        {
            IsFromProvider = false,
            IsFromSecretsManagerTrial = false,
            Coupons = ["COUPON_ONE", "COUPON_TWO"]
        };

        var sale = OrganizationSale.From(organization, signup);

        Assert.NotNull(sale.CustomerSetup);
        Assert.Equal(new[] { "COUPON_ONE", "COUPON_TWO" }, sale.CustomerSetup.DiscountCoupons);
        Assert.Null(sale.CustomerSetup.SystemCoupons);
    }

    [Fact]
    public void From_WithNoCoupons_CustomerSetupDiscountCouponsAndSystemCouponsAreNull()
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
        Assert.Null(sale.CustomerSetup.DiscountCoupons);
        Assert.Null(sale.CustomerSetup.SystemCoupons);
    }
}
