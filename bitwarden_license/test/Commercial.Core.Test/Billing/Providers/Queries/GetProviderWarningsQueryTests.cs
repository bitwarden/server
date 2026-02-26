using Bit.Commercial.Core.Billing.Providers.Queries;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Stripe.Tax;
using Xunit;

namespace Bit.Commercial.Core.Test.Billing.Providers.Queries;

using static StripeConstants;

[SutProviderCustomize]
public class GetProviderWarningsQueryTests
{
    private static readonly string[] _requiredExpansions = ["customer.tax_ids"];

    [Theory, BitAutoData]
    public async Task Run_NoSubscription_NoWarnings(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .ReturnsNull();

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            Suspension: null,
            TaxId: null
        });
    }

    [Theory, BitAutoData]
    public async Task Run_ProviderEnabled_NoSuspensionWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Unpaid,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration> { Data = [] });

        var response = await sutProvider.Sut.Run(provider);

        Assert.Null(response!.Suspension);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_SuspensionWarning_AddPaymentMethod(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = false;
        var cancelAt = DateTime.UtcNow.AddDays(7);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Unpaid,
                CancelAt = cancelAt,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration> { Data = [] });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            Suspension.Resolution: "add_payment_method"
        });
        Assert.Equal(cancelAt, response.Suspension.SubscriptionCancelsAt);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_SuspensionWarning_ContactAdministrator(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Unpaid,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(false);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration> { Data = [] });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            Suspension.Resolution: "contact_administrator"
        });
        Assert.Null(response.Suspension.SubscriptionCancelsAt);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_SuspensionWarning_ContactSupport(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Canceled,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration> { Data = [] });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            Suspension.Resolution: "contact_support"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_NotProviderAdmin_NoTaxIdWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(false);

        var response = await sutProvider.Sut.Run(provider);

        Assert.Null(response!.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_NoTaxRegistrationForCountry_NoTaxIdWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "GB" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.Null(response!.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_TaxIdMissingWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "CA" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_missing"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_TaxIdVerificationIsNull_NoTaxIdWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId>
                    {
                        Data = [new TaxId { Verification = null }]
                    },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "CA" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.Null(response!.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_TaxIdPendingVerificationWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId>
                    {
                        Data = [new TaxId
                        {
                            Verification = new TaxIdVerification
                            {
                                Status = TaxIdVerificationStatus.Pending
                            }
                        }]
                    },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "CA" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_pending_verification"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_TaxIdFailedVerificationWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId>
                    {
                        Data = [new TaxId
                        {
                            Verification = new TaxIdVerification
                            {
                                Status = TaxIdVerificationStatus.Unverified
                            }
                        }]
                    },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "CA" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_failed_verification"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_TaxIdVerified_NoTaxIdWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId>
                    {
                        Data = [new TaxId
                        {
                            Verification = new TaxIdVerification
                            {
                                Status = TaxIdVerificationStatus.Verified
                            }
                        }]
                    },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "CA" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.Null(response!.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_MultipleRegistrations_MatchesCorrectCountry(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "DE" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Is<RegistrationListOptions>(opt => opt.Status == TaxRegistrationStatus.Active))
            .Returns(new StripeList<Registration>
            {
                Data = [
                    new Registration { Country = "US" },
                    new Registration { Country = "DE" },
                    new Registration { Country = "FR" }
                ]
            });
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Is<RegistrationListOptions>(opt => opt.Status == TaxRegistrationStatus.Scheduled))
            .Returns(new StripeList<Registration> { Data = [] });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_missing"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_CombinesBothWarningTypes(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = false;
        var cancelAt = DateTime.UtcNow.AddDays(5);

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Unpaid,
                CancelAt = cancelAt,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "CA" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "CA" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.True(response is
        {
            Suspension.Resolution: "add_payment_method",
            TaxId.Type: "tax_id_missing"
        });
        Assert.Equal(cancelAt, response.Suspension.SubscriptionCancelsAt);
    }

    [Theory, BitAutoData]
    public async Task Run_USCustomer_NoTaxIdWarning(
        Provider provider,
        SutProvider<GetProviderWarningsQuery> sutProvider)
    {
        provider.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Active,
                Customer = new Customer
                {
                    TaxIds = new StripeList<TaxId> { Data = [] },
                    Address = new Address { Country = "US" }
                }
            });

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id).Returns(true);
        sutProvider.GetDependency<IStripeAdapter>().ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = [new Registration { Country = "US" }]
            });

        var response = await sutProvider.Sut.Run(provider);

        Assert.Null(response!.TaxId);
    }
}
