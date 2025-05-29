using Bit.Api.Billing.Queries.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Stripe.TestHelpers;
using Xunit;

namespace Bit.Api.Test.Billing.Queries.Organizations;

[SutProviderCustomize]
public class OrganizationWarningsQueryTests
{
    private static readonly string[] _requiredExpansions = ["customer", "latest_invoice", "test_clock"];

    [Theory, BitAutoData]
    public async Task Run_NoSubscription_NoWarnings(
        Organization organization,
        SutProvider<OrganizationWarningsQuery> sutProvider)
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = StripeConstants.SubscriptionStatus.Trialing,
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

        var response = await sutProvider.Sut.Run(organization);

        Assert.True(response is
        {
            FreeTrial.RemainingTrialDays: 7
        });
    }

    [Theory, BitAutoData]
    public async Task Run_Has_InactiveSubscriptionWarning_ContactProvider(
        Organization organization,
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = StripeConstants.SubscriptionStatus.Unpaid
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = StripeConstants.SubscriptionStatus.Unpaid
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = StripeConstants.SubscriptionStatus.Canceled
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        organization.Enabled = false;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                Status = StripeConstants.SubscriptionStatus.Unpaid
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
                Status = StripeConstants.SubscriptionStatus.Active,
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
    {
        var now = DateTime.UtcNow;

        sutProvider.GetDependency<ISubscriberService>()
            .GetSubscription(organization, Arg.Is<SubscriptionGetOptions>(options =>
                options.Expand.SequenceEqual(_requiredExpansions)
            ))
            .Returns(new Subscription
            {
                CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
                Status = StripeConstants.SubscriptionStatus.Active,
                LatestInvoice = new Invoice
                {
                    Status = StripeConstants.InvoiceStatus.Open,
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
        SutProvider<OrganizationWarningsQuery> sutProvider)
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
                CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
                Status = StripeConstants.SubscriptionStatus.PastDue,
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
}
