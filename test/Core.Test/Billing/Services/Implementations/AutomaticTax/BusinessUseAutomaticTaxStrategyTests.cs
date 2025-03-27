﻿using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services.Implementations.AutomaticTax;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Services.Implementations.AutomaticTax;

[SutProviderCustomize]
public class BusinessUseAutomaticTaxStrategyTests
{
    [Theory]
    [BitAutoData]
    public void GetUpdateOptions_ReturnsNull_WhenFeatureFlagAllowingToUpdateSubscriptionsIsDisabled(
        SutProvider<BusinessUseAutomaticTaxStrategy> sutProvider)
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
        SutProvider<BusinessUseAutomaticTaxStrategy> sutProvider)
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
        SutProvider<BusinessUseAutomaticTaxStrategy> sutProvider)
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
    [BitAutoData]
    public void GetUpdateOptions_SetsAutomaticTaxToTrue_ForAmericanCustomers(
        SutProvider<BusinessUseAutomaticTaxStrategy> sutProvider)
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

        Assert.NotNull(actual);
        Assert.True(actual.AutomaticTax.Enabled);
    }

    [Theory]
    [BitAutoData]
    public void GetUpdateOptions_SetsAutomaticTaxToTrue_ForGlobalCustomersWithTaxIds(
        SutProvider<BusinessUseAutomaticTaxStrategy> sutProvider)
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
                    Country = "ES",
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
    [BitAutoData]
    public void GetUpdateOptions_SetsAutomaticTaxToTrue_ForGlobalCustomersWithoutTaxIds(
        SutProvider<BusinessUseAutomaticTaxStrategy> sutProvider)
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
                    Country = "ES",
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
        Assert.False(actual.AutomaticTax.Enabled);
    }
}
