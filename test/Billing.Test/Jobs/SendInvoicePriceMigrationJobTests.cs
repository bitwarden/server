using Bit.Billing.Jobs;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Jobs;

public class SendInvoicePriceMigrationJobTests
{
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _cohortAssignmentRepository;
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripeAdapter _stripeAdapter;
    private readonly IBusinessPlanMigrationCoordinator _businessPlanMigrationCoordinator;
    private readonly IFeatureService _featureService;
    private readonly ILogger<SendInvoicePriceMigrationJob> _logger;
    private readonly SendInvoicePriceMigrationJob _sut;

    public SendInvoicePriceMigrationJobTests()
    {
        _cohortAssignmentRepository = Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
        _cohortRepository = Substitute.For<IOrganizationPlanMigrationCohortRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _stripeAdapter = Substitute.For<IStripeAdapter>();
        _businessPlanMigrationCoordinator = Substitute.For<IBusinessPlanMigrationCoordinator>();
        _featureService = Substitute.For<IFeatureService>();
        _logger = Substitute.For<ILogger<SendInvoicePriceMigrationJob>>();

        _featureService.IsEnabled(FeatureFlagKeys.PM38728_SendInvoicePriceMigration).Returns(true);

        // Candidates belong to a migration cohort unless a test overrides with a churn-only or
        // missing cohort; assignments are created with random cohort ids, so match any.
        _cohortRepository.GetByIdAsync(Arg.Any<Guid>()).Returns(Cohort());

        _sut = new SendInvoicePriceMigrationJob(
            _cohortAssignmentRepository,
            _cohortRepository,
            _organizationRepository,
            _stripeAdapter,
            _businessPlanMigrationCoordinator,
            _featureService,
            _logger);
    }

    [Fact]
    public async Task ExecuteJobAsync_FeatureFlagDisabled_DoesNothing()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM38728_SendInvoicePriceMigration).Returns(false);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _cohortAssignmentRepository.DidNotReceiveWithAnyArgs()
            .GetSendInvoiceCandidatesInWindowAsync(default, default);
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default, default);
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_QueriesRepositoryWithConfiguredWindow()
    {
        // Arrange
        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([]);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _cohortAssignmentRepository.Received(1).GetSendInvoiceCandidatesInWindowAsync(7, 15);
    }

    [Fact]
    public async Task ExecuteJobAsync_NoCandidates_DoesNotCallStripe()
    {
        // Arrange
        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([]);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default, default);
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_EligibleCandidate_InvokesCoordinatorWithOrganizationAndSubscription()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _stripeAdapter.Received(1).GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>());
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization, subscription);
    }

    [Fact]
    public async Task ExecuteJobAsync_SubscriptionFetch_RequestsRequiredExpansions()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        SubscriptionGetOptions? capturedOptions = null;

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter
            .GetSubscriptionAsync("sub_123", Arg.Do<SubscriptionGetOptions>(options => capturedOptions = options))
            .Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.Contains("discounts.source.coupon", capturedOptions.Expand);
        Assert.Contains("customer.discount.source.coupon", capturedOptions.Expand);
        Assert.Contains("test_clock", capturedOptions.Expand);
        // customer.discount implies customer; a separate customer entry would be redundant.
        Assert.DoesNotContain("customer", capturedOptions.Expand);
    }

    [Fact]
    public async Task ExecuteJobAsync_TestClockSubscription_EvaluatesWindowAgainstFrozenTime()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");

        // Renewal is ~1 year of real time away (outside the real-time window) but 10 days from the
        // clock's frozen time — the QA test-clock scenario after advancing toward renewal.
        var frozenTime = DateTime.UtcNow.AddDays(355);
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            frozenTime.AddDays(10));
        subscription.TestClock = new Stripe.TestHelpers.TestClock { FrozenTime = frozenTime };

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization, subscription);
    }

    [Fact]
    public async Task ExecuteJobAsync_SubscriptionUsesChargeAutomatically_SkipsOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.ChargeAutomatically,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_SubscriptionCancelsAtPeriodEnd_SkipsOrganization()
    {
        // Arrange: the customer asked to end the subscription — migrating would email them a renewal
        // price change and create a schedule extending a year past the cancellation.
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        subscription.CancelAtPeriodEnd = true;

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_SubscriptionHasFutureCancelAt_SkipsOrganization()
    {
        // Arrange: a future-dated cancel_at does not always surface as CancelAtPeriodEnd but still
        // means the subscription will not renew.
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        subscription.CancelAt = DateTime.UtcNow.AddDays(10);

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Theory]
    [InlineData(StripeConstants.SubscriptionStatus.Canceled)]
    [InlineData(StripeConstants.SubscriptionStatus.Unpaid)]
    [InlineData(StripeConstants.SubscriptionStatus.Incomplete)]
    [InlineData(StripeConstants.SubscriptionStatus.IncompleteExpired)]
    [InlineData(StripeConstants.SubscriptionStatus.Paused)]
    public async Task ExecuteJobAsync_SubscriptionInNonRenewingStatus_SkipsOrganization(string status)
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        subscription.Status = status;

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Theory]
    [InlineData(StripeConstants.SubscriptionStatus.Trialing)]
    [InlineData(StripeConstants.SubscriptionStatus.PastDue)]
    public async Task ExecuteJobAsync_SubscriptionInRenewingNonActiveStatus_InvokesCoordinator(string status)
    {
        // Arrange: past_due stays eligible for parity with the webhook path — Stripe still emits
        // invoice.upcoming for past_due subscriptions, and send-invoice customers routinely sit
        // past_due between invoice issue and payment.
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        subscription.Status = status;

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization, subscription);
    }

    [Fact]
    public async Task ExecuteJobAsync_RenewalDateBeforeWindow_SkipsOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(3));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);

        // This org renews at the old price with no notice — Error is the alerting hook.
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("cannot be migrated this cycle")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task ExecuteJobAsync_RenewalDateAfterWindow_SkipsOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(40));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_NullRenewalDate_SkipsOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");

        // No subscription items means GetCurrentPeriodEnd() cannot resolve a renewal date.
        var subscription = new Subscription
        {
            Id = "sub_123",
            CollectionMethod = StripeConstants.CollectionMethod.SendInvoice,
            Status = StripeConstants.SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_OrganizationNotFound_SkipsWithoutStripeCall()
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns((Organization?)null);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default, default);
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_OrganizationMissingGatewaySubscriptionId_SkipsWithoutStripeCall()
    {
        // Arrange
        var organizationId = Guid.NewGuid();

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(Organization(organizationId, null));

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default, default);
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_ChurnOnlyCohort_SkipsWithoutStripeCall()
    {
        // Arrange: a null MigrationPathId marks a churn-only cohort — nothing to schedule, and the
        // organization must not receive a renewal price-change email.
        var organizationId = Guid.NewGuid();
        var assignment = Assignment(organizationId);

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([assignment]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(Organization(organizationId, "sub_123"));
        _cohortRepository.GetByIdAsync(assignment.CohortId).Returns(Cohort(migrationPathId: null));

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default, default);
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_CohortNoLongerExists_SkipsWithoutStripeCall()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var assignment = Assignment(organizationId);

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([assignment]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(Organization(organizationId, "sub_123"));
        _cohortRepository.GetByIdAsync(assignment.CohortId).Returns((OrganizationPlanMigrationCohort?)null);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _stripeAdapter.DidNotReceiveWithAnyArgs().GetSubscriptionAsync(default, default);
        await _businessPlanMigrationCoordinator.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default);
    }

    [Fact]
    public async Task ExecuteJobAsync_SchedulerDeclines_LogsError()
    {
        // Arrange: NotScheduled can mean a Stripe schedule exists from a previous run but the
        // ScheduledDate stamp failed — the customer would be migrated without notice, so it alerts.
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.NotScheduled);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("declined the business plan price migration")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task ExecuteJobAsync_OneOrganizationThrows_ContinuesProcessingOthers()
    {
        // Arrange
        var organization1Id = Guid.NewGuid();
        var organization2Id = Guid.NewGuid();
        var organization3Id = Guid.NewGuid();

        var organization1 = Organization(organization1Id, "sub_1");
        var organization2 = Organization(organization2Id, "sub_2");
        var organization3 = Organization(organization3Id, "sub_3");

        var subscription1 = Subscription("sub_1", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        var subscription2 = Subscription("sub_2", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        var subscription3 = Subscription("sub_3", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organization1Id), Assignment(organization2Id), Assignment(organization3Id)]);

        _organizationRepository.GetByIdAsync(organization1Id).Returns(organization1);
        _organizationRepository.GetByIdAsync(organization2Id).Returns(organization2);
        _organizationRepository.GetByIdAsync(organization3Id).Returns(organization3);

        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(subscription1);
        _stripeAdapter.GetSubscriptionAsync("sub_2", Arg.Any<SubscriptionGetOptions>()).Returns(subscription2);
        _stripeAdapter.GetSubscriptionAsync("sub_3", Arg.Any<SubscriptionGetOptions>()).Returns(subscription3);

        _businessPlanMigrationCoordinator.ExecuteAsync(organization1, subscription1)
            .Returns(BusinessPlanMigrationResult.Completed);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization2, subscription2)
            .ThrowsAsync(new Exception("Scheduling failed"));
        _businessPlanMigrationCoordinator.ExecuteAsync(organization3, subscription3)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization1, subscription1);
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization3, subscription3);
    }

    [Fact]
    public async Task ExecuteJobAsync_StripeFetchThrowsForOneOrganization_ContinuesProcessingOthers()
    {
        // Arrange
        var organization1Id = Guid.NewGuid();
        var organization2Id = Guid.NewGuid();
        var organization3Id = Guid.NewGuid();

        var organization1 = Organization(organization1Id, "sub_1");
        var organization2 = Organization(organization2Id, "sub_2");
        var organization3 = Organization(organization3Id, "sub_3");

        var subscription1 = Subscription("sub_1", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        var subscription3 = Subscription("sub_3", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organization1Id), Assignment(organization2Id), Assignment(organization3Id)]);

        _organizationRepository.GetByIdAsync(organization1Id).Returns(organization1);
        _organizationRepository.GetByIdAsync(organization2Id).Returns(organization2);
        _organizationRepository.GetByIdAsync(organization3Id).Returns(organization3);

        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(subscription1);
        _stripeAdapter.GetSubscriptionAsync("sub_2", Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException("No such subscription"));
        _stripeAdapter.GetSubscriptionAsync("sub_3", Arg.Any<SubscriptionGetOptions>()).Returns(subscription3);

        _businessPlanMigrationCoordinator.ExecuteAsync(Arg.Any<Organization>(), Arg.Any<Subscription>())
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization1, subscription1);
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization3, subscription3);
    }

    [Fact]
    public async Task ExecuteJobAsync_ManyCandidates_RespectsMaxConcurrency()
    {
        // Arrange
        var assignments = Enumerable.Range(1, 30)
            .Select(_ => Assignment(Guid.NewGuid()))
            .ToList();

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(assignments);

        foreach (var assignment in assignments)
        {
            var subscriptionId = $"sub_{assignment.OrganizationId}";
            var organization = Organization(assignment.OrganizationId, subscriptionId);
            _organizationRepository.GetByIdAsync(assignment.OrganizationId).Returns(organization);
            _stripeAdapter.GetSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionGetOptions>())
                .Returns(Subscription(subscriptionId, StripeConstants.CollectionMethod.SendInvoice,
                    DateTime.UtcNow.AddDays(10)));
        }

        var concurrentCalls = 0;
        var maxConcurrentCalls = 0;
        var lockObj = new object();

        _businessPlanMigrationCoordinator.ExecuteAsync(Arg.Any<Organization>(), Arg.Any<Subscription>())
            .Returns(_ =>
            {
                lock (lockObj)
                {
                    concurrentCalls++;
                    if (concurrentCalls > maxConcurrentCalls)
                    {
                        maxConcurrentCalls = concurrentCalls;
                    }
                }

                return Task.Delay(50).ContinueWith(_ =>
                {
                    lock (lockObj)
                    {
                        concurrentCalls--;
                    }

                    return BusinessPlanMigrationResult.Completed;
                });
            });

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        Assert.True(maxConcurrentCalls <= 10, $"Expected max concurrency of 10, but got {maxConcurrentCalls}");
        // Guards against accidental serialization (e.g. SemaphoreSlim(1)); task-based, so not thread-flaky.
        Assert.True(maxConcurrentCalls > 1, "Expected candidates to be processed concurrently");
        await _businessPlanMigrationCoordinator.Received(30)
            .ExecuteAsync(Arg.Any<Organization>(), Arg.Any<Subscription>());
    }

    [Fact]
    public async Task ExecuteJobAsync_CompletedWithoutNotification_DoesNotThrowAndProcessesRemaining()
    {
        // Arrange
        var organization1Id = Guid.NewGuid();
        var organization2Id = Guid.NewGuid();

        var organization1 = Organization(organization1Id, "sub_1");
        var organization2 = Organization(organization2Id, "sub_2");

        var subscription1 = Subscription("sub_1", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        var subscription2 = Subscription("sub_2", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organization1Id), Assignment(organization2Id)]);

        _organizationRepository.GetByIdAsync(organization1Id).Returns(organization1);
        _organizationRepository.GetByIdAsync(organization2Id).Returns(organization2);

        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(subscription1);
        _stripeAdapter.GetSubscriptionAsync("sub_2", Arg.Any<SubscriptionGetOptions>()).Returns(subscription2);

        _businessPlanMigrationCoordinator.ExecuteAsync(organization1, subscription1)
            .Returns(BusinessPlanMigrationResult.CompletedWithoutNotification);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization2, subscription2)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization1, subscription1);
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization2, subscription2);

        // The Error level is the alerting hook for a customer migrated without being notified.
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("no renewal notification was sent")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task ExecuteJobAsync_CompletedWithoutNotificationOnLastEligibleDay_LogsManualNotificationRequired()
    {
        // Arrange: renewal exactly at the 7-day floor — tomorrow's sweep will no longer select this
        // organization, so this run is the last chance to send the notification automatically.
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");

        var frozenTime = DateTime.UtcNow.AddDays(100);
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            frozenTime.AddDays(7));
        subscription.TestClock = new Stripe.TestHelpers.TestClock { FrozenTime = frozenTime };

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.CompletedWithoutNotification);

        // Act
        await _sut.Execute(CreateContext());

        // Assert: the escalated message, not the routine will-retry one.
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("about to leave the retry window")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory]
    [InlineData(BusinessPlanMigrationResult.NotAssigned)]
    [InlineData(BusinessPlanMigrationResult.AlreadyMigrated)]
    [InlineData(BusinessPlanMigrationResult.NotScheduled)]
    public async Task ExecuteJobAsync_NonCommittingResults_DoNotHaltSweep(BusinessPlanMigrationResult result)
    {
        // Arrange
        var organization1Id = Guid.NewGuid();
        var organization2Id = Guid.NewGuid();

        var organization1 = Organization(organization1Id, "sub_1");
        var organization2 = Organization(organization2Id, "sub_2");

        var subscription1 = Subscription("sub_1", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));
        var subscription2 = Subscription("sub_2", StripeConstants.CollectionMethod.SendInvoice,
            DateTime.UtcNow.AddDays(10));

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organization1Id), Assignment(organization2Id)]);

        _organizationRepository.GetByIdAsync(organization1Id).Returns(organization1);
        _organizationRepository.GetByIdAsync(organization2Id).Returns(organization2);

        _stripeAdapter.GetSubscriptionAsync("sub_1", Arg.Any<SubscriptionGetOptions>()).Returns(subscription1);
        _stripeAdapter.GetSubscriptionAsync("sub_2", Arg.Any<SubscriptionGetOptions>()).Returns(subscription2);

        _businessPlanMigrationCoordinator.ExecuteAsync(organization1, subscription1).Returns(result);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization2, subscription2)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization2, subscription2);
    }

    [Fact]
    public async Task ExecuteJobAsync_MixedBatch_SummaryCountsMigratedSkippedAndErrors()
    {
        // Arrange: Completed and CompletedWithoutNotification count as migrated, NotScheduled as
        // skipped, and a throwing coordinator as an error.
        var results = new (Guid OrganizationId, BusinessPlanMigrationResult? Result)[]
        {
            (Guid.NewGuid(), BusinessPlanMigrationResult.Completed),
            (Guid.NewGuid(), BusinessPlanMigrationResult.CompletedWithoutNotification),
            (Guid.NewGuid(), BusinessPlanMigrationResult.NotScheduled),
            (Guid.NewGuid(), null)
        };

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(results.Select(r => Assignment(r.OrganizationId)).ToList());

        foreach (var (organizationId, result) in results)
        {
            var subscriptionId = $"sub_{organizationId}";
            var organization = Organization(organizationId, subscriptionId);
            var subscription = Subscription(subscriptionId, StripeConstants.CollectionMethod.SendInvoice,
                DateTime.UtcNow.AddDays(10));

            _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
            _stripeAdapter.GetSubscriptionAsync(subscriptionId, Arg.Any<SubscriptionGetOptions>())
                .Returns(subscription);

            if (result is null)
            {
                _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
                    .ThrowsAsync(new Exception("Scheduling failed"));
            }
            else
            {
                _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
                    .Returns(result.Value);
            }
        }

        // Act
        await _sut.Execute(CreateContext());

        // Assert: the summary line is the operational signal ops watches during rollout.
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Migrated: 2, Skipped: 1, Errors: 1")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(15)]
    public async Task ExecuteJobAsync_RenewalExactlyAtWindowBoundary_InvokesCoordinator(int leadTimeDays)
    {
        // Arrange: the window is inclusive at both ends, matching the selection stored procedure's
        // >= / <= predicates. A frozen test clock makes the boundary deterministic (no wall-clock race).
        var organizationId = Guid.NewGuid();
        var organization = Organization(organizationId, "sub_123");

        var frozenTime = DateTime.UtcNow.AddDays(100);
        var subscription = Subscription("sub_123", StripeConstants.CollectionMethod.SendInvoice,
            frozenTime.AddDays(leadTimeDays));
        subscription.TestClock = new Stripe.TestHelpers.TestClock { FrozenTime = frozenTime };

        _cohortAssignmentRepository.GetSendInvoiceCandidatesInWindowAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns([Assignment(organizationId)]);
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _stripeAdapter.GetSubscriptionAsync("sub_123", Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        _businessPlanMigrationCoordinator.ExecuteAsync(organization, subscription)
            .Returns(BusinessPlanMigrationResult.Completed);

        // Act
        await _sut.Execute(CreateContext());

        // Assert
        await _businessPlanMigrationCoordinator.Received(1).ExecuteAsync(organization, subscription);
    }

    [Fact]
    public void GetTrigger_ReturnsDailyCronTrigger()
    {
        // Act
        var trigger = SendInvoicePriceMigrationJob.GetTrigger();

        // Assert
        var cronTrigger = Assert.IsAssignableFrom<ICronTrigger>(trigger);
        Assert.Equal("0 0 8 * * ?", cronTrigger.CronExpressionString);
        Assert.Equal("SendInvoicePriceMigrationTrigger", trigger.Key.Name);
    }

    private static IJobExecutionContext CreateContext()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private static OrganizationPlanMigrationCohort Cohort(
        MigrationPathId? migrationPathId = MigrationPathId.Teams2020AnnualToCurrent) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "test-cohort",
            MigrationPathId = migrationPathId,
            IsActive = true
        };

    private static OrganizationPlanMigrationCohortAssignment Assignment(Guid organizationId) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CohortId = Guid.NewGuid()
        };

    private static Organization Organization(Guid organizationId, string? gatewaySubscriptionId) =>
        new()
        {
            Id = organizationId,
            GatewaySubscriptionId = gatewaySubscriptionId
        };

    private static Subscription Subscription(string id, string collectionMethod, DateTime currentPeriodEnd) =>
        new()
        {
            Id = id,
            CollectionMethod = collectionMethod,
            Status = StripeConstants.SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { CurrentPeriodEnd = currentPeriodEnd }]
            }
        };
}
