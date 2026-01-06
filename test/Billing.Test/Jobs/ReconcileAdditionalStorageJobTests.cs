using Bit.Billing.Jobs;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.Billing.Constants;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Jobs;

public class ReconcileAdditionalStorageJobTests
{
    private readonly IStripeFacade _stripeFacade;
    private readonly ILogger<ReconcileAdditionalStorageJob> _logger;
    private readonly IFeatureService _featureService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;
    private readonly ReconcileAdditionalStorageJob _sut;

    public ReconcileAdditionalStorageJobTests()
    {
        _stripeFacade = Substitute.For<IStripeFacade>();
        _logger = Substitute.For<ILogger<ReconcileAdditionalStorageJob>>();
        _featureService = Substitute.For<IFeatureService>();
        _userRepository = Substitute.For<IUserRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _stripeEventUtilityService = Substitute.For<IStripeEventUtilityService>();

        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, null, null));

        _sut = new ReconcileAdditionalStorageJob(
            _stripeFacade,
            _logger,
            _featureService,
            _userRepository,
            _organizationRepository,
            _stripeEventUtilityService);
    }

    #region Feature Flag Tests

    [Fact]
    public async Task Execute_FeatureFlagDisabled_SkipsProcessing()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob)
            .Returns(false);

        // Act
        await _sut.Execute(context);

        // Assert
        _stripeFacade.DidNotReceiveWithAnyArgs().ListSubscriptionsAutoPagingAsync();
    }

    [Fact]
    public async Task Execute_FeatureFlagEnabled_ProcessesSubscriptions()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob)
            .Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode)
            .Returns(false);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Empty<Subscription>());

        // Act
        await _sut.Execute(context);

        // Assert
        _stripeFacade.Received(3).ListSubscriptionsAutoPagingAsync(
            Arg.Is<SubscriptionListOptions>(o => o.Limit == 100));
    }

    #endregion

    #region Dry Run Mode Tests

    [Fact]
    public async Task Execute_DryRunMode_DoesNotUpdateSubscriptions()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(false); // Dry run ON

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    [Fact]
    public async Task Execute_DryRunMode_DoesNotUpdateDatabase()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(false); // Dry run ON

        // Create a personal subscription that would normally trigger a database update
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
        subscription.Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() };

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Mock GetIdsFromMetadata to return userId
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        // Act
        await _sut.Execute(context);

        // Assert - Verify database repositories are never called
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _organizationRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Fact]
    public async Task Execute_DryRunModeDisabled_UpdatesSubscriptions()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true); // Dry run OFF

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o => o.Items.Count == 1));
    }

    [Fact]
    public async Task Execute_LiveMode_PersonalSubscription_UpdatesUserDatabase()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        // Setup user
        var userId = Guid.NewGuid();
        var user = new Bit.Core.Entities.User
        {
            Id = userId,
            Email = "test@example.com",
            GatewaySubscriptionId = "sub_personal",
            MaxStorageGb = 15 // Old value
        };
        _userRepository.GetByIdAsync(userId).Returns(user);
        _userRepository.ReplaceAsync(user).Returns(Task.CompletedTask);

        // Create personal subscription with premium seat + 10 GB storage (will be reduced to 6 GB)
        var subscription = CreateSubscriptionWithMultipleItems("sub_personal",
            [("premium-annually", 1L), ("storage-gb-monthly", 10L)]);
        subscription.Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() };

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Mock GetIdsFromMetadata to return userId
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        // Act
        await _sut.Execute(context);

        // Assert - Verify Stripe update happened
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_personal",
            Arg.Is<SubscriptionUpdateOptions>(o => o.Items.Count == 1 && o.Items[0].Quantity == 6));

        // Assert - Verify database update with correct MaxStorageGb (5 included + 6 new quantity = 11)
        await _userRepository.Received(1).GetByIdAsync(userId);
        await _userRepository.Received(1).ReplaceAsync(user);
        Assert.Equal((short)11, user.MaxStorageGb);
    }

    [Fact]
    public async Task Execute_LiveMode_OrganizationSubscription_UpdatesOrganizationDatabase()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        // Setup organization
        var organizationId = Guid.NewGuid();
        var organization = new Bit.Core.AdminConsole.Entities.Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            GatewaySubscriptionId = "sub_org",
            MaxStorageGb = 13 // Old value
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _organizationRepository.ReplaceAsync(organization).Returns(Task.CompletedTask);

        // Create organization subscription with org seat plan + 8 GB storage (will be reduced to 4 GB)
        var subscription = CreateSubscriptionWithMultipleItems("sub_org",
            [("2023-teams-org-seat-annually", 5L), ("storage-gb-monthly", 8L)]);
        subscription.Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() };

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Mock GetIdsFromMetadata to return organizationId
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(organizationId, null, null));

        // Act
        await _sut.Execute(context);

        // Assert - Verify Stripe update happened
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_org",
            Arg.Is<SubscriptionUpdateOptions>(o => o.Items.Count == 1 && o.Items[0].Quantity == 4));

        // Assert - Verify database update with correct MaxStorageGb (5 included + 4 new quantity = 9)
        await _organizationRepository.Received(1).GetByIdAsync(organizationId);
        await _organizationRepository.Received(1).ReplaceAsync(organization);
        Assert.Equal((short)9, organization.MaxStorageGb);
    }

    [Fact]
    public async Task Execute_LiveMode_StorageItemDeleted_UpdatesDatabaseWithBaseStorage()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        // Setup user
        var userId = Guid.NewGuid();
        var user = new Bit.Core.Entities.User
        {
            Id = userId,
            Email = "test@example.com",
            GatewaySubscriptionId = "sub_delete",
            MaxStorageGb = 8 // Old value
        };
        _userRepository.GetByIdAsync(userId).Returns(user);
        _userRepository.ReplaceAsync(user).Returns(Task.CompletedTask);

        // Create personal subscription with premium seat + 3 GB storage (will be deleted since 3 < 4)
        var subscription = CreateSubscriptionWithMultipleItems("sub_delete",
            [("premium-annually", 1L), ("storage-gb-monthly", 3L)]);
        subscription.Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() };

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Mock GetIdsFromMetadata to return userId
        _stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata)
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        // Act
        await _sut.Execute(context);

        // Assert - Verify Stripe update happened (item deleted)
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_delete",
            Arg.Is<SubscriptionUpdateOptions>(o => o.Items.Count == 1 && o.Items[0].Deleted == true));

        // Assert - Verify database update with base storage only (5 GB)
        await _userRepository.Received(1).GetByIdAsync(userId);
        await _userRepository.Received(1).ReplaceAsync(user);
        Assert.Equal((short)5, user.MaxStorageGb);
    }

    #endregion

    #region Price ID Processing Tests

    [Fact]
    public async Task Execute_ProcessesAllThreePriceIds()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(false);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Empty<Subscription>());

        // Act
        await _sut.Execute(context);

        // Assert
        _stripeFacade.Received(1).ListSubscriptionsAutoPagingAsync(
            Arg.Is<SubscriptionListOptions>(o => o.Price == "storage-gb-monthly"));
        _stripeFacade.Received(1).ListSubscriptionsAutoPagingAsync(
            Arg.Is<SubscriptionListOptions>(o => o.Price == "storage-gb-annually"));
        _stripeFacade.Received(1).ListSubscriptionsAutoPagingAsync(
            Arg.Is<SubscriptionListOptions>(o => o.Price == "personal-storage-gb-annually"));
    }

    #endregion

    #region Already Processed Tests

    [Fact]
    public async Task Execute_SubscriptionAlreadyProcessed_SkipsUpdate()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var metadata = new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.StorageReconciled2025] = DateTime.UtcNow.ToString("o")
        };
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, metadata: metadata);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    [Fact]
    public async Task Execute_SubscriptionWithInvalidProcessedDate_ProcessesSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.StorageReconciled2025] = "invalid-date"
        };
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, metadata: metadata);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_123", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Execute_SubscriptionWithoutMetadata_ProcessesSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, metadata: null);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_123", Arg.Any<SubscriptionUpdateOptions>());
    }

    #endregion

    #region Quantity Reduction Logic Tests

    [Fact]
    public async Task Execute_QuantityGreaterThan4_ReducesBy4()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 1 &&
                o.Items[0].Quantity == 6 &&
                o.Items[0].Deleted != true));
    }

    [Fact]
    public async Task Execute_QuantityEquals4_DeletesItem()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 4);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 1 &&
                o.Items[0].Deleted == true));
    }

    [Fact]
    public async Task Execute_QuantityLessThan4_DeletesItem()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 2);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Count == 1 &&
                o.Items[0].Deleted == true));
    }

    #endregion

    #region Update Options Tests

    [Fact]
    public async Task Execute_UpdateOptions_SetsProrationBehaviorToCreateProrations()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o => o.ProrationBehavior == StripeConstants.ProrationBehavior.CreateProrations));
    }

    [Fact]
    public async Task Execute_UpdateOptions_SetsReconciledMetadata()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Metadata.ContainsKey(StripeConstants.MetadataKeys.StorageReconciled2025) &&
                !string.IsNullOrEmpty(o.Metadata[StripeConstants.MetadataKeys.StorageReconciled2025])));
    }

    #endregion

    #region Subscription Filtering Tests

    [Fact]
    public async Task Execute_SubscriptionWithNoItems_SkipsUpdate()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = new Subscription
        {
            Id = "sub_123",
            Items = null
        };

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    [Fact]
    public async Task Execute_SubscriptionWithDifferentPriceId_SkipsUpdate()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = CreateSubscription("sub_123", "different-price-id", quantity: 10);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    [Fact]
    public async Task Execute_NullSubscription_SkipsProcessing()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create<Subscription>(null!));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    #endregion

    #region Multiple Subscriptions Tests

    [Fact]
    public async Task Execute_MultipleSubscriptions_ProcessesAll()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);
        var subscription2 = CreateSubscription("sub_2", "storage-gb-monthly", quantity: 5);
        var subscription3 = CreateSubscription("sub_3", "storage-gb-monthly", quantity: 3);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription1, subscription2, subscription3));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(callInfo => callInfo.Arg<string>() switch
            {
                "sub_1" => subscription1,
                "sub_2" => subscription2,
                "sub_3" => subscription3,
                _ => null
            });

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_1", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_2", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_3", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Execute_MixedSubscriptionsWithProcessed_OnlyProcessesUnprocessed()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var processedMetadata = new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.StorageReconciled2025] = DateTime.UtcNow.ToString("o")
        };

        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);
        var subscription2 = CreateSubscription("sub_2", "storage-gb-monthly", quantity: 5, metadata: processedMetadata);
        var subscription3 = CreateSubscription("sub_3", "storage-gb-monthly", quantity: 3);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription1, subscription2, subscription3));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(callInfo => callInfo.Arg<string>() switch
            {
                "sub_1" => subscription1,
                "sub_3" => subscription3,
                _ => null
            });

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_1", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.DidNotReceive().UpdateSubscription("sub_2", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_3", Arg.Any<SubscriptionUpdateOptions>());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Execute_UpdateFails_ContinuesProcessingOthers()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);
        var subscription2 = CreateSubscription("sub_2", "storage-gb-monthly", quantity: 5);
        var subscription3 = CreateSubscription("sub_3", "storage-gb-monthly", quantity: 3);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription1, subscription2, subscription3));

        _stripeFacade.UpdateSubscription("sub_1", Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription1);
        _stripeFacade.UpdateSubscription("sub_2", Arg.Any<SubscriptionUpdateOptions>())
            .Throws(new Exception("Stripe API error"));
        _stripeFacade.UpdateSubscription("sub_3", Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription3);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_1", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_2", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_3", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Execute_UpdateFails_LogsError()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Throws(new Exception("Stripe API error"));

        // Act
        await _sut.Execute(context);

        // Assert
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    #endregion

    #region Subscription Status Filtering Tests

    [Fact]
    public async Task Execute_ActiveStatusSubscription_ProcessesSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, status: StripeConstants.SubscriptionStatus.Active);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_123", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Execute_TrialingStatusSubscription_ProcessesSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, status: StripeConstants.SubscriptionStatus.Trialing);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_123", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Execute_PastDueStatusSubscription_ProcessesSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, status: StripeConstants.SubscriptionStatus.PastDue);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_123", Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Execute_CanceledStatusSubscription_SkipsSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, status: StripeConstants.SubscriptionStatus.Canceled);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    [Fact]
    public async Task Execute_IncompleteStatusSubscription_SkipsSubscription()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, status: StripeConstants.SubscriptionStatus.Incomplete);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null!);
    }

    [Fact]
    public async Task Execute_MixedSubscriptionStatuses_OnlyProcessesValidStatuses()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var userId = Guid.NewGuid();
        var activeSubscription = CreateSubscription("sub_active", "storage-gb-monthly", quantity: 10, status: StripeConstants.SubscriptionStatus.Active);
        var trialingSubscription = CreateSubscription("sub_trialing", "storage-gb-monthly", quantity: 8, status: StripeConstants.SubscriptionStatus.Trialing);
        var pastDueSubscription = CreateSubscription("sub_pastdue", "storage-gb-monthly", quantity: 6, status: StripeConstants.SubscriptionStatus.PastDue);
        var canceledSubscription = CreateSubscription("sub_canceled", "storage-gb-monthly", quantity: 5, status: StripeConstants.SubscriptionStatus.Canceled);
        var incompleteSubscription = CreateSubscription("sub_incomplete", "storage-gb-monthly", quantity: 4, status: StripeConstants.SubscriptionStatus.Incomplete);
        _stripeEventUtilityService.GetIdsFromMetadata(Arg.Any<Dictionary<string, string>>())
            .Returns(Tuple.Create<Guid?, Guid?, Guid?>(null, userId, null));

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(activeSubscription, trialingSubscription, pastDueSubscription, canceledSubscription, incompleteSubscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(callInfo => callInfo.Arg<string>() switch
            {
                "sub_active" => activeSubscription,
                "sub_trialing" => trialingSubscription,
                "sub_pastdue" => pastDueSubscription,
                _ => null
            });

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription("sub_active", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_trialing", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.Received(1).UpdateSubscription("sub_pastdue", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.DidNotReceive().UpdateSubscription("sub_canceled", Arg.Any<SubscriptionUpdateOptions>());
        await _stripeFacade.DidNotReceive().UpdateSubscription("sub_incomplete", Arg.Any<SubscriptionUpdateOptions>());
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Execute_CancellationRequested_LogsWarningAndExits()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        var context = CreateJobExecutionContext(cts.Token);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription1));

        // Act
        await _sut.Execute(context);

        // Assert - Should not process any subscriptions due to immediate cancellation
        await _stripeFacade.DidNotReceiveWithAnyArgs().UpdateSubscription(null);
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    #endregion

    #region Helper Method Tests

    #region DetermineSubscriptionPlanTier Tests

    [Fact]
    public void DetermineSubscriptionPlanTier_WithUserId_ReturnsPersonal()
    {
        // Arrange
        var userId = Guid.NewGuid();
        Guid? organizationId = null;

        // Act
        var result = _sut.DetermineSubscriptionPlanTier(userId, organizationId);

        // Assert
        Assert.Equal(ReconcileAdditionalStorageJob.SubscriptionPlanTier.Personal, result);
    }

    [Fact]
    public void DetermineSubscriptionPlanTier_WithOrganizationId_ReturnsOrganization()
    {
        // Arrange
        Guid? userId = null;
        var organizationId = Guid.NewGuid();

        // Act
        var result = _sut.DetermineSubscriptionPlanTier(userId, organizationId);

        // Assert
        Assert.Equal(ReconcileAdditionalStorageJob.SubscriptionPlanTier.Organization, result);
    }

    [Fact]
    public void DetermineSubscriptionPlanTier_WithBothIds_ReturnsPersonal()
    {
        // Arrange - Personal takes precedence
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        // Act
        var result = _sut.DetermineSubscriptionPlanTier(userId, organizationId);

        // Assert
        Assert.Equal(ReconcileAdditionalStorageJob.SubscriptionPlanTier.Personal, result);
    }

    [Fact]
    public void DetermineSubscriptionPlanTier_WithNoIds_ReturnsUnknown()
    {
        // Arrange
        Guid? userId = null;
        Guid? organizationId = null;

        // Act
        var result = _sut.DetermineSubscriptionPlanTier(userId, organizationId);

        // Assert
        Assert.Equal(ReconcileAdditionalStorageJob.SubscriptionPlanTier.Unknown, result);
    }

    #endregion

    #region GetCurrentStorageQuantityFromSubscription Tests

    [Theory]
    [InlineData("storage-gb-monthly", 10L, 10L)]
    [InlineData("storage-gb-annually", 25L, 25L)]
    [InlineData("personal-storage-gb-annually", 5L, 5L)]
    [InlineData("storage-gb-monthly", 0L, 0L)]
    public void GetCurrentStorageQuantityFromSubscription_WithMatchingPriceId_ReturnsQuantity(
        string priceId, long quantity, long expectedQuantity)
    {
        // Arrange
        var subscription = CreateSubscription("sub_123", priceId, quantity);

        // Act
        var result = _sut.GetCurrentStorageQuantityFromSubscription(subscription, priceId);

        // Assert
        Assert.Equal(expectedQuantity, result);
    }

    [Fact]
    public void GetCurrentStorageQuantityFromSubscription_WithNonMatchingPriceId_ReturnsZero()
    {
        // Arrange
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", 10L);

        // Act
        var result = _sut.GetCurrentStorageQuantityFromSubscription(subscription, "different-price-id");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetCurrentStorageQuantityFromSubscription_WithNullItems_ReturnsZero()
    {
        // Arrange
        var subscription = new Subscription { Id = "sub_123", Items = null };

        // Act
        var result = _sut.GetCurrentStorageQuantityFromSubscription(subscription, "storage-gb-monthly");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetCurrentStorageQuantityFromSubscription_WithEmptyItems_ReturnsZero()
    {
        // Arrange
        var subscription = new Subscription
        {
            Id = "sub_123",
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        // Act
        var result = _sut.GetCurrentStorageQuantityFromSubscription(subscription, "storage-gb-monthly");

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region CalculateNewMaxStorageGb Tests

    [Theory]
    [InlineData(10L, 6L, 11)] // 5 included + 6 new quantity
    [InlineData(15L, 11L, 16)] // 5 included + 11 new quantity
    [InlineData(4L, 0L, 5)] // Item deleted, returns base storage
    [InlineData(2L, 0L, 5)] // Item deleted, returns base storage
    [InlineData(8L, 4L, 9)] // 5 included + 4 new quantity
    public void CalculateNewMaxStorageGb_WithQuantityUpdate_ReturnsCorrectMaxStorage(
        long currentQuantity, long newQuantity, short expectedMaxStorageGb)
    {
        // Arrange
        var updateOptions = new SubscriptionUpdateOptions
        {
            Items =
            [
                newQuantity == 0
                    ? new SubscriptionItemOptions { Id = "si_123", Deleted = true } // Item marked as deleted
                    : new SubscriptionItemOptions { Id = "si_123", Quantity = newQuantity } // Item quantity updated
            ]
        };

        // Act
        var result = _sut.CalculateNewMaxStorageGb(currentQuantity, updateOptions);

        // Assert
        Assert.Equal(expectedMaxStorageGb, result);
    }

    [Fact]
    public void CalculateNewMaxStorageGb_WithNullUpdateOptions_ReturnsCurrentQuantityPlusBaseIncluded()
    {
        // Arrange
        const long currentQuantity = 10;

        // Act
        var result = _sut.CalculateNewMaxStorageGb(currentQuantity, null);

        // Assert
        Assert.Equal((short)(5 + currentQuantity), result);
    }

    [Fact]
    public void CalculateNewMaxStorageGb_WithNullItems_ReturnsCurrentQuantityPlusBaseIncluded()
    {
        // Arrange
        const long currentQuantity = 10;
        var updateOptions = new SubscriptionUpdateOptions { Items = null };

        // Act
        var result = _sut.CalculateNewMaxStorageGb(currentQuantity, updateOptions);

        // Assert
        Assert.Equal(5 + currentQuantity, result);
    }

    [Fact]
    public void CalculateNewMaxStorageGb_WithEmptyItems_ReturnsCurrentQuantity()
    {
        // Arrange
        const long currentQuantity = 10;
        var updateOptions = new SubscriptionUpdateOptions
        {
            Items = []
        };

        // Act
        var result = _sut.CalculateNewMaxStorageGb(currentQuantity, updateOptions);

        // Assert
        Assert.Equal((short)currentQuantity, result);
    }

    [Fact]
    public void CalculateNewMaxStorageGb_WithDeletedItem_ReturnsBaseStorage()
    {
        // Arrange
        const long currentQuantity = 100;
        var updateOptions = new SubscriptionUpdateOptions
        {
            Items = [new SubscriptionItemOptions { Id = "si_123", Deleted = true }]
        };

        // Act
        var result = _sut.CalculateNewMaxStorageGb(currentQuantity, updateOptions);

        // Assert
        Assert.Equal((short)5, result); // Base storage
    }

    [Fact]
    public void CalculateNewMaxStorageGb_WithItemWithoutQuantity_ReturnsCurrentQuantity()
    {
        // Arrange
        const long currentQuantity = 10;
        var updateOptions = new SubscriptionUpdateOptions
        {
            Items = [new SubscriptionItemOptions { Id = "si_123", Quantity = null }]
        };

        // Act
        var result = _sut.CalculateNewMaxStorageGb(currentQuantity, updateOptions);

        // Assert
        Assert.Equal((short)currentQuantity, result);
    }

    #endregion

    #region UpdateDatabaseMaxStorageAsync Tests

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_PersonalTier_UpdatesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new Bit.Core.Entities.User
        {
            Id = userId,
            Email = "test@example.com",
            GatewaySubscriptionId = "sub_123"
        };
        _userRepository.GetByIdAsync(userId).Returns(user);
        _userRepository.ReplaceAsync(user).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Personal,
            userId,
            10,
            "sub_123");

        // Assert
        Assert.True(result);
        Assert.Equal((short)10, user.MaxStorageGb);
        await _userRepository.Received(1).GetByIdAsync(userId);
        await _userRepository.Received(1).ReplaceAsync(user);
    }

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_PersonalTier_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdAsync(userId).Returns((Bit.Core.Entities.User?)null);

        // Act
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Personal,
            userId,
            10,
            "sub_123");

        // Assert
        Assert.False(result);
        await _userRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_PersonalTier_ReplaceThrowsException_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new Bit.Core.Entities.User
        {
            Id = userId,
            Email = "test@example.com",
            GatewaySubscriptionId = "sub_123"
        };
        _userRepository.GetByIdAsync(userId).Returns(user);
        _userRepository.ReplaceAsync(user).Throws(new Exception("Database error"));

        // Act
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Personal,
            userId,
            10,
            "sub_123");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_OrganizationTier_UpdatesOrganization()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = new Bit.Core.AdminConsole.Entities.Organization
        {
            Id = organizationId,
            Name = "Test Org",
            GatewaySubscriptionId = "sub_456"
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _organizationRepository.ReplaceAsync(organization).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Organization,
            organizationId,
            20,
            "sub_456");

        // Assert
        Assert.True(result);
        Assert.Equal((short)20, organization.MaxStorageGb);
        await _organizationRepository.Received(1).GetByIdAsync(organizationId);
        await _organizationRepository.Received(1).ReplaceAsync(organization);
    }

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_OrganizationTier_OrganizationNotFound_ReturnsFalse()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId)
            .Returns((Bit.Core.AdminConsole.Entities.Organization?)null);

        // Act
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Organization,
            organizationId,
            20,
            "sub_456");

        // Assert
        Assert.False(result);
        await _organizationRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_OrganizationTier_ReplaceThrowsException_ReturnsFalse()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var organization = new Bit.Core.AdminConsole.Entities.Organization
        {
            Id = organizationId,
            Name = "Test Org",
            GatewaySubscriptionId = "sub_456"
        };
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);
        _organizationRepository.ReplaceAsync(organization).Throws(new Exception("Database error"));

        // Act
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Organization,
            organizationId,
            20,
            "sub_456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateDatabaseMaxStorageAsync_UnknownTier_ReturnsFalse()
    {
        // Arrange & Act
        var entityId = Guid.NewGuid();
        var result = await _sut.UpdateDatabaseMaxStorageAsync(
            ReconcileAdditionalStorageJob.SubscriptionPlanTier.Unknown,
            entityId,
            15,
            "sub_789");

        // Assert
        Assert.False(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    #endregion

    #endregion

    #region Helper Methods

    private static IJobExecutionContext CreateJobExecutionContext(CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }

    private static Subscription CreateSubscription(
        string id,
        string priceId,
        long? quantity = null,
        Dictionary<string, string>? metadata = null,
        string status = StripeConstants.SubscriptionStatus.Active)
    {
        var price = new Price { Id = priceId };
        var item = new SubscriptionItem
        {
            Id = $"si_{id}",
            Price = price,
            Quantity = quantity ?? 0
        };

        return new Subscription
        {
            Id = id,
            Status = status,
            Metadata = metadata,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [item]
            }
        };
    }

    private static Subscription CreateSubscriptionWithMultipleItems(string id, (string priceId, long quantity)[] items)
    {
        var subscriptionItems = items.Select(i => new SubscriptionItem
        {
            Id = $"si_{id}_{i.priceId}",
            Price = new Price { Id = i.priceId },
            Quantity = i.quantity
        }).ToList();

        return new Subscription
        {
            Id = id,
            Status = StripeConstants.SubscriptionStatus.Active,
            Items = new StripeList<SubscriptionItem>
            {
                Data = subscriptionItems
            }
        };
    }

    #endregion
}

internal static class AsyncEnumerable
{
    public static async IAsyncEnumerable<T> Create<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    public static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
