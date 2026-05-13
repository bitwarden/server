using Bit.Billing.Constants;
using Bit.Billing.Jobs;
using Bit.Billing.Services;
using Bit.Billing.Services.Implementations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Models.BitStripe;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Stripe;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;
using Event = Stripe.Event;

namespace Bit.Billing.Test.Services;

public class SubscriptionDeletedHandlerTests
{
    private readonly IStripeEventService _stripeEventService;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderService _providerService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IPushNotificationAdapter _pushNotificationAdapter;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IScheduler _scheduler;
    private readonly SubscriptionDeletedHandler _sut;

    public SubscriptionDeletedHandlerTests()
    {
        _stripeEventService = Substitute.For<IStripeEventService>();
        _userService = Substitute.For<IUserService>();
        _userRepository = Substitute.For<IUserRepository>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _providerService = Substitute.For<IProviderService>();
        _schedulerFactory = Substitute.For<ISchedulerFactory>();
        _pushNotificationAdapter = Substitute.For<IPushNotificationAdapter>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _scheduler = Substitute.For<IScheduler>();
        _schedulerFactory.GetScheduler().Returns(_scheduler);
        _sut = new SubscriptionDeletedHandler(
            _stripeEventService,
            _userService,
            _userRepository,
            _stripeEventUtilityService,
            _organizationDisableCommand,
            _providerRepository,
            _providerService,
            _schedulerFactory,
            _pushNotificationAdapter,
            _stripeAdapter,
            Substitute.For<ILogger<SubscriptionDeletedHandler>>());
    }

    [Fact]
    public async Task HandleAsync_SubscriptionNotCanceled_DoesNothing()
    {
        // Arrange
        var stripeEvent = new Event();
        var subscription = new Subscription
        {
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string>()
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs().DisableAsync(default, default);
        await _userService.DidNotReceiveWithAnyArgs().DisablePremiumAsync(default, default);
        await _providerService.DidNotReceiveWithAnyArgs().UpdateAsync(default);
    }

    [Fact]
    public async Task HandleAsync_OrganizationSubscriptionCanceled_DisablesOrganization()
    {
        // Arrange
        var stripeEvent = new Event();
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.Received(1)
            .DisableAsync(organizationId, subscription.GetCurrentPeriodEnd());
    }

    [Fact]
    public async Task HandleAsync_UserSubscriptionCanceled_DisablesUserPremium()
    {
        // Arrange
        var stripeEvent = new Event();
        var userId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "userId", userId.ToString() } }
        };

        var user = new User { Id = userId, Premium = false, PremiumExpirationDate = subscription.GetCurrentPeriodEnd() };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _userService.Received(1)
            .DisablePremiumAsync(userId, subscription.GetCurrentPeriodEnd());
        await _userRepository.Received(1).GetByIdAsync(userId);
        await _pushNotificationAdapter.Received(1).NotifyPremiumStatusChangedAsync(user);
    }

    [Fact]
    public async Task HandleAsync_ProviderMigrationCancellation_DoesNotDisableOrganization()
    {
        // Arrange
        var stripeEvent = new Event();
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            CancellationDetails = new SubscriptionCancellationDetails
            {
                Comment = "Cancelled as part of provider migration to Consolidated Billing"
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs()
            .DisableAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_AddedToProviderCancellation_DoesNotDisableOrganization()
    {
        // Arrange
        var stripeEvent = new Event();
        var organizationId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } },
            CancellationDetails = new SubscriptionCancellationDetails
            {
                Comment = "Organization was added to Provider"
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs()
            .DisableAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_ProviderSubscriptionCanceled_DisablesProviderAndQueuesJob()
    {
        // Arrange
        var stripeEvent = new Event();
        var providerId = Guid.NewGuid();
        var provider = new Provider
        {
            Id = providerId,
            Enabled = true
        };
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns(provider);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j => j.JobType == typeof(ProviderOrganizationDisableJob)),
            Arg.Any<ITrigger>());
    }

    [Fact]
    public async Task HandleAsync_ProviderSubscriptionCanceled_ProviderNotFound_DoesNotThrow()
    {
        // Arrange
        var stripeEvent = new Event();
        var providerId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns((Provider)null);

        // Act & Assert - Should not throw
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _providerService.DidNotReceiveWithAnyArgs().UpdateAsync(default);
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleJob(default, default);
    }

    [Fact]
    public async Task HandleAsync_ProviderSubscriptionCanceled_QueuesJobWithCorrectParameters()
    {
        // Arrange
        var stripeEvent = new Event();
        var providerId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(30);
        var provider = new Provider
        {
            Id = providerId,
            Enabled = true
        };
        var subscription = new Subscription
        {
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { CurrentPeriodEnd = expirationDate }
                ]
            },
            Metadata = new Dictionary<string, string> { { "providerId", providerId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, providerId));
        _providerRepository.GetByIdAsync(providerId).Returns(provider);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        Assert.False(provider.Enabled);
        await _providerService.Received(1).UpdateAsync(provider);
        await _scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j =>
                j.JobType == typeof(ProviderOrganizationDisableJob) &&
                j.JobDataMap.GetString("providerId") == providerId.ToString() &&
                j.JobDataMap.GetString("expirationDate") == expirationDate.ToString("O")),
            Arg.Is<ITrigger>(t => t.Key.Name == $"disable-trigger-{providerId}"));
    }

    [Fact]
    public async Task HandleAsync_CanceledWithUnpaidLifecycleMetadata_VoidsAllOpenInvoices()
    {
        // Arrange
        var stripeEvent = new Event { Id = "evt_unpaid_void" };
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_unpaid_canceled";
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() },
                { MetadataKeys.CancellationOrigin, CancellationOrigins.UnpaidSubscription }
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));
        _stripeAdapter.ListInvoicesAsync(Arg.Is<StripeInvoiceListOptions>(o =>
                o.Subscription == subscriptionId &&
                o.Status == InvoiceStatus.Open &&
                o.SelectAll))
            .Returns(new List<Invoice>
            {
                new() { Id = "in_001" },
                new() { Id = "in_002" }
            });

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _stripeAdapter.Received(1).VoidInvoiceAsync("in_001");
        await _stripeAdapter.Received(1).VoidInvoiceAsync("in_002");
    }

    [Fact]
    public async Task HandleAsync_CanceledWithoutUnpaidLifecycleMetadata_DoesNotVoidInvoices()
    {
        // The regression-critical negative case: a voluntary or off-platform cancel must
        // leave open invoices intact for ops to reconcile manually. The metadata gate is
        // the sole signal we use to distinguish platform-managed cancellations from others.
        var stripeEvent = new Event { Id = "evt_voluntary_cancel" };
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_voluntary_canceled";
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }]
            },
            Metadata = new Dictionary<string, string> { { "organizationId", organizationId.ToString() } }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().ListInvoicesAsync(default!);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().VoidInvoiceAsync(default!);
    }

    [Fact]
    public async Task HandleAsync_CanceledWithUnpaidLifecycleMetadata_VoidInvoiceThrows_ContinuesWithRemainingInvoices()
    {
        // Webhook re-delivery hitting an already-voided invoice is the most likely
        // per-invoice failure. The loop should continue rather than abandon the rest.
        var stripeEvent = new Event { Id = "evt_void_stripe_failure" };
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_void_stripe_failure";
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() },
                { MetadataKeys.CancellationOrigin, CancellationOrigins.UnpaidSubscription }
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));
        _stripeAdapter.ListInvoicesAsync(Arg.Any<StripeInvoiceListOptions>())
            .Returns(new List<Invoice>
            {
                new() { Id = "in_already_voided" },
                new() { Id = "in_still_open" }
            });
        _stripeAdapter.VoidInvoiceAsync("in_already_voided")
            .Throws(new StripeException("Invoice cannot be voided"));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _stripeAdapter.Received(1).VoidInvoiceAsync("in_already_voided");
        await _stripeAdapter.Received(1).VoidInvoiceAsync("in_still_open");
    }

    [Fact]
    public async Task HandleAsync_CanceledWithUnpaidLifecycleMetadata_VoidInvoiceThrowsTransportError_ContinuesWithRemainingInvoices()
    {
        // Non-Stripe exceptions (HttpRequestException from socket reset,
        // TaskCanceledException from SDK timeout, etc.) must also not abandon the loop.
        var stripeEvent = new Event { Id = "evt_void_transport_failure" };
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_void_transport_failure";
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = DateTime.UtcNow.AddDays(30) }]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() },
                { MetadataKeys.CancellationOrigin, CancellationOrigins.UnpaidSubscription }
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));
        _stripeAdapter.ListInvoicesAsync(Arg.Any<StripeInvoiceListOptions>())
            .Returns(new List<Invoice>
            {
                new() { Id = "in_transport_fail" },
                new() { Id = "in_recovers" }
            });
        _stripeAdapter.VoidInvoiceAsync("in_transport_fail")
            .Throws(new HttpRequestException("Connection reset"));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _stripeAdapter.Received(1).VoidInvoiceAsync("in_transport_fail");
        await _stripeAdapter.Received(1).VoidInvoiceAsync("in_recovers");
    }

    [Fact]
    public async Task HandleAsync_CanceledWithUnpaidLifecycleMetadata_ListInvoicesThrows_DoesNotBlockSubscriberDisable()
    {
        // A Stripe outage during ListInvoices must not prevent the subscriber-disable
        // path from running. Voiding is best-effort cleanup; disabling is customer-protective.
        var stripeEvent = new Event { Id = "evt_list_failure" };
        var organizationId = Guid.NewGuid();
        const string subscriptionId = "sub_list_failure";
        var currentPeriodEnd = DateTime.UtcNow.AddDays(30);
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = StripeSubscriptionStatus.Canceled,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            },
            Metadata = new Dictionary<string, string>
            {
                { "organizationId", organizationId.ToString() },
                { MetadataKeys.CancellationOrigin, CancellationOrigins.UnpaidSubscription }
            }
        };

        _stripeEventService.GetSubscription(stripeEvent, true).Returns(subscription);
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));
        _stripeAdapter.ListInvoicesAsync(Arg.Any<StripeInvoiceListOptions>())
            .Throws(new StripeException("Stripe upstream timeout"));

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _organizationDisableCommand.Received(1)
            .DisableAsync(organizationId, subscription.GetCurrentPeriodEnd());
        await _stripeAdapter.DidNotReceiveWithAnyArgs().VoidInvoiceAsync(default!);
    }
}
