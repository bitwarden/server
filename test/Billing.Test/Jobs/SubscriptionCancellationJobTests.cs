using Bit.Billing.Constants;
using Bit.Billing.Jobs;
using Bit.Billing.Services;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Jobs;

public class SubscriptionCancellationJobTests
{
    private readonly IStripeFacade _stripeFacade;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly SubscriptionCancellationJob _sut;

    public SubscriptionCancellationJobTests()
    {
        _stripeFacade = Substitute.For<IStripeFacade>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _sut = new SubscriptionCancellationJob(_stripeFacade, _organizationRepository, Substitute.For<ILogger<SubscriptionCancellationJob>>());
    }

    [Fact]
    public async Task Execute_OrganizationIsNull_SkipsCancellation()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        _organizationRepository.GetByIdAsync(organizationId).Returns((Organization)null);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
        await _stripeFacade.DidNotReceiveWithAnyArgs().CancelSubscription(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());
    }

    [Fact]
    public async Task Execute_OrganizationIsEnabled_SkipsCancellation()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = true
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().GetSubscription(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
        await _stripeFacade.DidNotReceiveWithAnyArgs().CancelSubscription(Arg.Any<string>(), Arg.Any<SubscriptionCancelOptions>());
    }

    [Fact]
    public async Task Execute_SubscriptionStatusIsNotUnpaid_SkipsCancellation()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Active,
            LatestInvoice = new Invoice
            {
                BillingReason = "subscription_cycle"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceive().CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
    }

    [Fact]
    public async Task Execute_BillingReasonIsInvalid_SkipsCancellation()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            LatestInvoice = new Invoice
            {
                BillingReason = "manual"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceive().CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
    }

    [Fact]
    public async Task Execute_ValidConditions_CancelsSubscriptionAndVoidsInvoices()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            LatestInvoice = new Invoice
            {
                BillingReason = "subscription_cycle"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        var invoices = new StripeList<Invoice>
        {
            Data =
            [
                new Invoice { Id = "inv_1" },
                new Invoice { Id = "inv_2" }
            ],
            HasMore = false
        };
        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>()).Returns(invoices);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
        await _stripeFacade.Received(1).VoidInvoice("inv_1");
        await _stripeFacade.Received(1).VoidInvoice("inv_2");
    }

    [Fact]
    public async Task Execute_WithSubscriptionCreateBillingReason_CancelsSubscription()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            LatestInvoice = new Invoice
            {
                BillingReason = "subscription_create"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        var invoices = new StripeList<Invoice>
        {
            Data = [],
            HasMore = false
        };
        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>()).Returns(invoices);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
    }

    [Fact]
    public async Task Execute_NoOpenInvoices_CancelsSubscriptionOnly()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            LatestInvoice = new Invoice
            {
                BillingReason = "subscription_cycle"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        var invoices = new StripeList<Invoice>
        {
            Data = [],
            HasMore = false
        };
        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>()).Returns(invoices);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
        await _stripeFacade.DidNotReceiveWithAnyArgs().VoidInvoice(Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_WithPagination_VoidsAllInvoices()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            LatestInvoice = new Invoice
            {
                BillingReason = "subscription_cycle"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        // First page of invoices
        var firstPage = new StripeList<Invoice>
        {
            Data =
            [
                new Invoice { Id = "inv_1" },
                new Invoice { Id = "inv_2" }
            ],
            HasMore = true
        };

        // Second page of invoices
        var secondPage = new StripeList<Invoice>
        {
            Data =
            [
                new Invoice { Id = "inv_3" },
                new Invoice { Id = "inv_4" }
            ],
            HasMore = false
        };

        _stripeFacade.ListInvoices(Arg.Is<InvoiceListOptions>(o => o.StartingAfter == null))
            .Returns(firstPage);
        _stripeFacade.ListInvoices(Arg.Is<InvoiceListOptions>(o => o.StartingAfter == "inv_2"))
            .Returns(secondPage);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).CancelSubscription(subscriptionId, Arg.Any<SubscriptionCancelOptions>());
        await _stripeFacade.Received(1).VoidInvoice("inv_1");
        await _stripeFacade.Received(1).VoidInvoice("inv_2");
        await _stripeFacade.Received(1).VoidInvoice("inv_3");
        await _stripeFacade.Received(1).VoidInvoice("inv_4");
        await _stripeFacade.Received(2).ListInvoices(Arg.Any<InvoiceListOptions>());
    }

    [Fact]
    public async Task Execute_ListInvoicesCalledWithCorrectOptions()
    {
        // Arrange
        const string subscriptionId = "sub_123";
        var organizationId = Guid.NewGuid();
        var context = CreateJobExecutionContext(subscriptionId, organizationId);

        var organization = new Organization
        {
            Id = organizationId,
            Enabled = false
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Unpaid,
            LatestInvoice = new Invoice
            {
                BillingReason = "subscription_cycle"
            }
        };
        _stripeFacade.GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")))
            .Returns(subscription);

        var invoices = new StripeList<Invoice>
        {
            Data = [],
            HasMore = false
        };
        _stripeFacade.ListInvoices(Arg.Any<InvoiceListOptions>()).Returns(invoices);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).GetSubscription(subscriptionId, Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("latest_invoice")));
        await _stripeFacade.Received(1).ListInvoices(Arg.Is<InvoiceListOptions>(o =>
            o.Status == "open" &&
            o.Subscription == subscriptionId &&
            o.Limit == 100));
    }

    private static IJobExecutionContext CreateJobExecutionContext(string subscriptionId, Guid organizationId)
    {
        var context = Substitute.For<IJobExecutionContext>();
        var jobDataMap = new JobDataMap
        {
            { "subscriptionId", subscriptionId },
            { "organizationId", organizationId.ToString() }
        };
        context.MergedJobDataMap.Returns(jobDataMap);
        return context;
    }
}
