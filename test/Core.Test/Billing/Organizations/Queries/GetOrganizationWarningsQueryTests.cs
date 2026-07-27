using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Stripe.Tax;
using Stripe.TestHelpers;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Queries;

using static StripeConstants;

[SutProviderCustomize]
public class GetOrganizationWarningsQueryTests
{
    private static readonly string[] _requiredExpansions = ["customer.tax_ids", "latest_invoice", "test_clock"];

    [Theory, BitAutoData]
    public async Task Run_NoSubscription_NoWarnings(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .ReturnsNull();

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            FreeTrial: null,
            InactiveSubscription: null,
            ResellerRenewal: null,
            ScheduledPriceIncrease: null
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_FreeTrialWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Trialing,
                TrialEnd = now.AddDays(7),
                Customer = new Customer
                {
                    InvoiceSettings = new CustomerInvoiceSettings(),
                    Metadata = new Dictionary<string, string>()
                },
                TestClock = new TestClock
                {
                    FrozenTime = now
                }
            });

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organization.Id).Returns(true);
        sutProvider.GetDependency<IHasPaymentMethodQuery>().Run(organization).Returns(false);

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            FreeTrial.RemainingTrialDays: 7
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_FreeTrialWarning_WithPaymentMethod_NoWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Trialing,
                TrialEnd = now.AddDays(7),
                Customer = new Customer
                {
                    InvoiceSettings = new CustomerInvoiceSettings(),
                    Metadata = new Dictionary<string, string>()
                },
                TestClock = new TestClock
                {
                    FrozenTime = now
                }
            });

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organization.Id).Returns(true);
        sutProvider.GetDependency<IHasPaymentMethodQuery>().Run(organization).Returns(true);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.FreeTrial);
    }

    [Theory, BitAutoData]
    public async Task Run_OrganizationEnabled_NoInactiveSubscriptionWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = SubscriptionStatus.Unpaid,
                Customer = new Customer
                {
                    InvoiceSettings = new CustomerInvoiceSettings(),
                    Metadata = new Dictionary<string, string>()
                }
            });

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.InactiveSubscription);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_InactiveSubscriptionWarning_ContactProvider(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;
        organization.ExemptFromBillingAutomation = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Customer = new Customer(),
                Status = SubscriptionStatus.Unpaid
            });

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider());

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            InactiveSubscription.Resolution: "contact_provider"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_InactiveSubscriptionWarning_AddPaymentMethod(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;
        organization.ExemptFromBillingAutomation = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Customer = new Customer(),
                Status = SubscriptionStatus.Unpaid
            });

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            InactiveSubscription.Resolution: "add_payment_method"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_InactiveSubscriptionWarning_Resubscribe(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;
        organization.ExemptFromBillingAutomation = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Customer = new Customer(),
                Status = SubscriptionStatus.Canceled
            });

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            InactiveSubscription.Resolution: "resubscribe"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_InactiveSubscriptionWarning_ContactOwner(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;
        organization.ExemptFromBillingAutomation = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Customer = new Customer(),
                Status = SubscriptionStatus.Unpaid
            });

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(false);

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            InactiveSubscription.Resolution: "contact_owner"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_ResellerRenewalWarning_Upcoming(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.ExemptFromBillingAutomation = false;

        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                CollectionMethod = CollectionMethod.SendInvoice,
                Customer = new Customer(),
                Status = SubscriptionStatus.Active,
                Items = new StripeList<SubscriptionItem>
                {
                    Data =
                    [
                        new SubscriptionItem
                        {
                            CurrentPeriodEnd = now.AddDays(10)
                        }
                    ]
                },
                TestClock = new TestClock
                {
                    FrozenTime = now
                }
            });

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider
            {
                Type = ProviderType.Reseller
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            ResellerRenewal.Type: "upcoming"
        });

        Assert.Equal(now.AddDays(10), response.ResellerRenewal.Upcoming!.RenewalDate);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_ResellerRenewalWarning_Issued(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.ExemptFromBillingAutomation = false;

        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                CollectionMethod = CollectionMethod.SendInvoice,
                Customer = new Customer(),
                Status = SubscriptionStatus.Active,
                LatestInvoice = new Invoice
                {
                    Status = InvoiceStatus.Open,
                    DueDate = now.AddDays(30),
                    Created = now
                },
                TestClock = new TestClock
                {
                    FrozenTime = now
                }
            });

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider
            {
                Type = ProviderType.Reseller
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            ResellerRenewal.Type: "issued"
        });

        Assert.Equal(now, response.ResellerRenewal.Issued!.IssuedDate);
        Assert.Equal(now.AddDays(30), response.ResellerRenewal.Issued!.DueDate);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_ResellerRenewalWarning_PastDue(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.ExemptFromBillingAutomation = false;

        var now = DateTime.UtcNow;

        const string subscriptionId = "subscription_id";

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Id = subscriptionId,
                CollectionMethod = CollectionMethod.SendInvoice,
                Customer = new Customer(),
                Status = SubscriptionStatus.PastDue,
                TestClock = new TestClock
                {
                    FrozenTime = now
                }
            });

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider
            {
                Type = ProviderType.Reseller
            });

        var dueDate = now.AddDays(-10);

        sutProvider.GetDependency<IStripeAdapter>().SearchInvoiceAsync(Arg.Is<InvoiceSearchOptions>(options =>
            options.Query == $"subscription:'{subscriptionId}' status:'open'")).Returns([
            new Invoice { DueDate = dueDate, Created = dueDate.AddDays(-30) }
        ]);

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            ResellerRenewal.Type: "past_due"
        });

        Assert.Equal(dueDate.AddDays(30), response.ResellerRenewal.PastDue!.SuspensionDate);
    }

    [Theory, BitAutoData]
    public async Task Run_FreeCustomer_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.Free;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_NotOwner_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(false);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_HasProvider_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider());

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_NoRegistrationInCountry_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "GB" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_Has_TaxIdWarning_Missing(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_missing"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_TaxIdWarning_PendingVerification(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;

        var taxId = new TaxId
        {
            Verification = new TaxIdVerification
            {
                Status = TaxIdVerificationStatus.Pending
            }
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_pending_verification"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_TaxIdWarning_FailedVerification(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var taxId = new TaxId
        {
            Verification = new TaxIdVerification
            {
                Status = TaxIdVerificationStatus.Unverified
            }
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_failed_verification"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_VerifiedTaxId_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var taxId = new TaxId
        {
            Verification = new TaxIdVerification
            {
                Status = TaxIdVerificationStatus.Verified
            }
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_NullVerification_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var taxId = new TaxId
        {
            Verification = null
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_ExemptFromBillingAutomation_NoInactiveSubscriptionWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;
        organization.ExemptFromBillingAutomation = true;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Customer = new Customer(),
                Status = SubscriptionStatus.Unpaid
            });

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.InactiveSubscription);
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_USCustomer_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "US" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_TaxableCustomer_Has_TaxIdWarning_Missing(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "DE" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "DE" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_missing"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_ExemptCustomer_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.Exempt,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_ReverseCustomer_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.Reverse,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_NoRegistrationInCountry_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId>() },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "GB" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_TaxableCustomer_Has_TaxIdWarning_PendingVerification(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var taxId = new TaxId
        {
            Verification = new TaxIdVerification
            {
                Status = TaxIdVerificationStatus.Pending
            }
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_pending_verification"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_TaxableCustomer_Has_TaxIdWarning_FailedVerification(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var taxId = new TaxId
        {
            Verification = new TaxIdVerification
            {
                Status = TaxIdVerificationStatus.Unverified
            }
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            TaxId.Type: "tax_id_failed_verification"
        });
    }

    [Theory, BitAutoData]
    public async Task Run_FlagEnabled_TaxableCustomer_VerifiedTaxId_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.PlanType = PlanType.TeamsAnnually;

        var taxId = new TaxId
        {
            Verification = new TaxIdVerification
            {
                Status = TaxIdVerificationStatus.Verified
            }
        };

        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "CA" },
                TaxExempt = TaxExempt.None,
                TaxIds = new StripeList<TaxId> { Data = new List<TaxId> { taxId } },
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListTaxRegistrationsAsync(Arg.Any<RegistrationListOptions>())
            .Returns(new StripeList<Registration>
            {
                Data = new List<Registration>
                {
                    new() { Country = "CA" }
                }
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.TaxId);
    }

    [Theory, BitAutoData]
    public async Task Run_ExemptFromBillingAutomation_NoResellerRenewalWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        organization.ExemptFromBillingAutomation = true;

        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                CollectionMethod = CollectionMethod.SendInvoice,
                Customer = new Customer(),
                Status = SubscriptionStatus.Active,
                Items = new StripeList<SubscriptionItem>
                {
                    Data =
                    [
                        new SubscriptionItem
                        {
                            CurrentPeriodEnd = now.AddDays(10)
                        }
                    ]
                },
                TestClock = new TestClock
                {
                    FrozenTime = now
                }
            });

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id)
            .Returns(new Provider
            {
                Type = ProviderType.Reseller
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ResellerRenewal);
    }

    [Theory, BitAutoData]
    public async Task Run_ScheduledPriceIncrease_Monthly_ReturnsWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;
        var renewalDate = now.AddDays(10);

        SetupScheduledPriceIncrease(sutProvider, organization, now, renewalDate,
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        var response = await sutProvider.Sut.Run(organization);

        Assert.NotNull(response.ScheduledPriceIncrease);
        Assert.Equal("monthly", response.ScheduledPriceIncrease.Cadence);
        Assert.Equal(5M, response.ScheduledPriceIncrease.SeatPrice);
        Assert.Equal(renewalDate, response.ScheduledPriceIncrease.EffectiveDate);
    }

    [Theory, BitAutoData]
    public async Task Run_ScheduledPriceIncrease_Annual_ReturnsWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;
        var renewalDate = now.AddDays(20);

        SetupScheduledPriceIncrease(sutProvider, organization, now, renewalDate,
            MigrationPathId.Enterprise2020AnnualToCurrent, PlanType.EnterpriseAnnually);

        var response = await sutProvider.Sut.Run(organization);

        Assert.NotNull(response.ScheduledPriceIncrease);
        Assert.Equal("annually", response.ScheduledPriceIncrease.Cadence);
        // Enterprise annual seat price is 72/yr -> 6.00/mo.
        Assert.Equal(6M, response.ScheduledPriceIncrease.SeatPrice);
        Assert.Equal(renewalDate, response.ScheduledPriceIncrease.EffectiveDate);
    }

    [Theory, BitAutoData]
    public async Task Run_NoScheduleAttached_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
    }

    [Theory, BitAutoData]
    public async Task Run_SchedulePresentButNotMigration_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        var (_, assignment) = SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        // Churn-only cohort: null MigrationPathId.
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(assignment.CohortId)
            .Returns(new OrganizationPlanMigrationCohort { Id = assignment.CohortId, MigrationPathId = null });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
    }

    [Theory, BitAutoData]
    public async Task Run_NoCohortAssignment_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .ReturnsNull();

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive().ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
    }

    [Theory]
    [BitAutoData(SubscriptionStatus.Canceled)]
    [BitAutoData(SubscriptionStatus.PastDue)]
    [BitAutoData(SubscriptionStatus.Unpaid)]
    public async Task Run_InactiveSubscription_NullWarning(
        string status,
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly, status);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive().ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
    }

    [Theory, BitAutoData]
    public async Task Run_FeatureFlagOff_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(false);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive().ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceive().GetByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task Run_UpcomingPhaseSelection_BeforeRenewal_UsesTargetPhase(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;
        var renewalDate = now.AddDays(15);

        SetupScheduledPriceIncrease(sutProvider, organization, now, renewalDate,
            MigrationPathId.Enterprise2020AnnualToCurrent, PlanType.EnterpriseAnnually);

        // Normalized 3-phase schedule: anchor + current both start <= now and must be excluded.
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = "sub_123",
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = now.AddDays(-40), EndDate = now.AddDays(-30) },
                new SubscriptionSchedulePhase { StartDate = now.AddDays(-30), EndDate = renewalDate },
                new SubscriptionSchedulePhase { StartDate = renewalDate, EndDate = renewalDate.AddYears(1) }
            ]
        };
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var response = await sutProvider.Sut.Run(organization);

        Assert.NotNull(response.ScheduledPriceIncrease);
        Assert.Equal(renewalDate, response.ScheduledPriceIncrease.EffectiveDate);
    }

    [Theory, BitAutoData]
    public async Task Run_UnknownMigrationPathId_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        var (_, assignment) = SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        // A MigrationPathId value that MigrationPaths.FromId does not resolve.
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(assignment.CohortId)
            .Returns(new OrganizationPlanMigrationCohort
            {
                Id = assignment.CohortId,
                MigrationPathId = (MigrationPathId)200
            });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
    }

    [Theory, BitAutoData]
    public async Task Run_CallerCannotManageBilling_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        SetupScheduledPriceIncrease(sutProvider, organization, now, now.AddDays(10),
            MigrationPathId.Teams2020MonthlyToCurrent, PlanType.TeamsMonthly);

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organization.Id).Returns(false);

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive().ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>());
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceive().GetByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task Run_AfterRenewalBoundary_NullWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;
        var renewalDate = now.AddDays(-5);

        SetupScheduledPriceIncrease(sutProvider, organization, now, renewalDate,
            MigrationPathId.Enterprise2020AnnualToCurrent, PlanType.EnterpriseAnnually);

        // Schedule still Active (EndBehavior=Release): current phase already ended at the past
        // renewal; target phase [renewalDate, renewalDate + period] still present but starts <= now.
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = "sub_123",
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = now.AddDays(-370), EndDate = renewalDate },
                new SubscriptionSchedulePhase { StartDate = renewalDate, EndDate = now.AddDays(360) }
            ]
        };
        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var response = await sutProvider.Sut.Run(organization);

        Assert.Null(response.ScheduledPriceIncrease);
    }

    private static (Subscription Subscription, OrganizationPlanMigrationCohortAssignment Assignment) SetupScheduledPriceIncrease(
        SutProvider<GetOrganizationWarningsQuery> sutProvider,
        Organization organization,
        DateTime now,
        DateTime renewalDate,
        MigrationPathId migrationPathId,
        PlanType targetPlanType,
        string subscriptionStatus = SubscriptionStatus.Active)
    {
        var subscription = new Subscription
        {
            Id = "sub_123",
            CustomerId = "cus_123",
            Status = subscriptionStatus,
            Customer = new Customer
            {
                InvoiceSettings = new CustomerInvoiceSettings(),
                Metadata = new Dictionary<string, string>()
            },
            Metadata = new Dictionary<string, string>(),
            TestClock = new TestClock { FrozenTime = now }
        };

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)))
            .Returns(subscription);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().EditSubscription(organization.Id).Returns(true);

        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase { StartDate = now.AddDays(-30), EndDate = renewalDate },
                new SubscriptionSchedulePhase { StartDate = renewalDate, EndDate = renewalDate.AddYears(1) }
            ]
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid()
        };

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(assignment);

        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(assignment.CohortId)
            .Returns(new OrganizationPlanMigrationCohort
            {
                Id = assignment.CohortId,
                MigrationPathId = migrationPathId
            });

        sutProvider.GetDependency<IPricingClient>()
            .GetPlanOrThrow(targetPlanType)
            .Returns(MockPlans.Get(targetPlanType));

        return (subscription, assignment);
    }
}
