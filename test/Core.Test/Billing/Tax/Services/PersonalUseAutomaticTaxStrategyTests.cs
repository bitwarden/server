using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Tax.Services.Implementations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Tax.Services;

[SutProviderCustomize]
public class PersonalUseAutomaticTaxStrategyTests
{
    [Theory]
    [BitAutoData]
    public void GetUpdateOptions_ReturnsNull_WhenFeatureFlagAllowingToUpdateSubscriptionsIsDisabled(
        SutProvider<PersonalUseAutomaticTaxStrategy> sutProvider)
    {
        var subscription = new Subscription();

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
            .Returns(false);

        var actual = sutProvider.Sut.GetUpdateOptions(subscription);

        Assert.Null(actual);
    }

    [Theory]
    [BitAutoData]
    public void GetUpdateOptions_ReturnsNull_WhenSubscriptionDoesNotNeedUpdating(
        SutProvider<PersonalUseAutomaticTaxStrategy> sutProvider)
    {
        var subscription = new Subscription
        {
            AutomaticTax = new SubscriptionAutomaticTax
            {
                Enabled = true
            },
            Customer = new Customer
            {
                Address = new Address
                {
                    Country = "US",
                },
                Tax = new CustomerTax
                {
                    AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
                }
            }
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
            .Returns(true);

        var actual = sutProvider.Sut.GetUpdateOptions(subscription);

        Assert.Null(actual);
    }

    [Theory]
    [BitAutoData]
    public void GetUpdateOptions_SetsAutomaticTaxToFalse_WhenTaxLocationIsUnrecognizedOrInvalid(
        SutProvider<PersonalUseAutomaticTaxStrategy> sutProvider)
    {
        var subscription = new Subscription
        {
            AutomaticTax = new SubscriptionAutomaticTax
            {
                Enabled = true
            },
            Customer = new Customer
            {
                Tax = new CustomerTax
                {
                    AutomaticTax = StripeConstants.AutomaticTaxStatus.UnrecognizedLocation
                }
            }
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
            .Returns(true);

        var actual = sutProvider.Sut.GetUpdateOptions(subscription);

        Assert.NotNull(actual);
        Assert.False(actual.AutomaticTax.Enabled);
    }

    [Theory]
    [BitAutoData("CA")]
    [BitAutoData("ES")]
    [BitAutoData("US")]
    public void GetUpdateOptions_SetsAutomaticTaxToTrue_ForAllCountries(
        string country, SutProvider<PersonalUseAutomaticTaxStrategy> sutProvider)
    {
        var subscription = new Subscription
        {
            AutomaticTax = new SubscriptionAutomaticTax
            {
                Enabled = false
            },
            Customer = new Customer
            {
                Address = new Address
                {
                    Country = country
                },
                Tax = new CustomerTax
                {
                    AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
                }
            }
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
            .Returns(true);

        var actual = sutProvider.Sut.GetUpdateOptions(subscription);

        Assert.NotNull(actual);
        Assert.True(actual.AutomaticTax.Enabled);
    }

    [Theory]
    [BitAutoData("CA")]
    [BitAutoData("ES")]
    [BitAutoData("US")]
    public void GetUpdateOptions_SetsAutomaticTaxToTrue_ForGlobalCustomersWithTaxIds(
        string country, SutProvider<PersonalUseAutomaticTaxStrategy> sutProvider)
    {
        var subscription = new Subscription
        {
            AutomaticTax = new SubscriptionAutomaticTax
            {
                Enabled = false
            },
            Customer = new Customer
            {
                Address = new Address
                {
                    Country = country,
                },
                Tax = new CustomerTax
                {
                    AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
                },
                TaxIds = new StripeList<TaxId>
                {
                    Data = new List<TaxId>
                    {
                        new()
                        {
                            Country = "ES",
                            Type = "eu_vat",
                            Value = "ESZ8880999Z"
                        }
                    }
                }
            }
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
            .Returns(true);

        var actual = sutProvider.Sut.GetUpdateOptions(subscription);

        Assert.NotNull(actual);
        Assert.True(actual.AutomaticTax.Enabled);
    }

    [Theory]
    [BitAutoData("CA")]
    [BitAutoData("ES")]
    [BitAutoData("US")]
    public void GetUpdateOptions_SetsAutomaticTaxToTrue_ForGlobalCustomersWithoutTaxIds(
        string country, SutProvider<PersonalUseAutomaticTaxStrategy> sutProvider)
    {
        var subscription = new Subscription
        {
            AutomaticTax = new SubscriptionAutomaticTax
            {
                Enabled = false
            },
            Customer = new Customer
            {
                Address = new Address
                {
                    Country = country
                },
                Tax = new CustomerTax
                {
                    AutomaticTax = StripeConstants.AutomaticTaxStatus.Supported
                },
                TaxIds = new StripeList<TaxId>
                {
                    Data = new List<TaxId>()
                }
            }
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Arg.Is<string>(p => p == FeatureFlagKeys.PM19422_AllowAutomaticTaxUpdates))
            .Returns(true);

        var actual = sutProvider.Sut.GetUpdateOptions(subscription);

        Assert.NotNull(actual);
        Assert.True(actual.AutomaticTax.Enabled);
    }
}
