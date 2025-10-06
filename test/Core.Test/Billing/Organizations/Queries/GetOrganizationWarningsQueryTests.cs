﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
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
            ResellerRenewal: null
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
        sutProvider.GetDependency<ISetupIntentCache>().GetSetupIntentIdForSubscriber(organization.Id).Returns((string?)null);

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            FreeTrial.RemainingTrialDays: 7
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_FreeTrialWarning_WithUnverifiedBankAccount_NoWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;
        const string setupIntentId = "setup_intent_id";

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
        sutProvider.GetDependency<ISetupIntentCache>().GetSetupIntentIdForSubscriber(organization.Id).Returns(setupIntentId);
        sutProvider.GetDependency<IStripeAdapter>().SetupIntentGet(setupIntentId, Arg.Is<SetupIntentGetOptions>(
            options => options.Expand.Contains("payment_method"))).Returns(new SetupIntent
            {
                Status = "requires_action",
                NextAction = new SetupIntentNextAction
                {
                    VerifyWithMicrodeposits = new SetupIntentNextActionVerifyWithMicrodeposits()
                },
                PaymentMethod = new PaymentMethod
                {
                    UsBankAccount = new PaymentMethodUsBankAccount()
                }
            });

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
                CurrentPeriodEnd = now.AddDays(10),
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

        sutProvider.GetDependency<IStripeAdapter>().InvoiceSearchAsync(Arg.Is<InvoiceSearchOptions>(options =>
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
    public async Task Run_USCustomer_NoTaxIdWarning(
        Organization organization,
        SutProvider<GetOrganizationWarningsQuery> sutProvider)
    {
        var subscription = new Subscription
        {
            Customer = new Customer
            {
                Address = new Address { Country = "US" },
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
            .TaxRegistrationsListAsync(Arg.Any<RegistrationListOptions>())
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
            .TaxRegistrationsListAsync(Arg.Any<RegistrationListOptions>())
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
            .TaxRegistrationsListAsync(Arg.Any<RegistrationListOptions>())
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
            .TaxRegistrationsListAsync(Arg.Any<RegistrationListOptions>())
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
            .TaxRegistrationsListAsync(Arg.Any<RegistrationListOptions>())
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
            .TaxRegistrationsListAsync(Arg.Any<RegistrationListOptions>())
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
}
