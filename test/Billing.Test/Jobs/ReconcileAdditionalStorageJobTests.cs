using Bit.Billing.Jobs;
using Bit.Billing.Services;
using Bit.Core;
using Bit.Core.Billing.Constants;
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
    private readonly ReconcileAdditionalStorageJob _sut;

    public ReconcileAdditionalStorageJobTests()
    {
        _stripeFacade = Substitute.For<IStripeFacade>();
        _logger = Substitute.For<ILogger<ReconcileAdditionalStorageJob>>();
        _featureService = Substitute.For<IFeatureService>();
        _sut = new ReconcileAdditionalStorageJob(_stripeFacade, _logger, _featureService);
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
            Arg.Is<SubscriptionListOptions>(o => o.Status == "active"));
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
    public async Task Execute_DryRunModeDisabled_UpdatesSubscriptions()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true); // Dry run OFF

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);
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

        var metadata = new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.StorageReconciled2025] = "invalid-date"
        };
        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, metadata: metadata);

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

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10, metadata: null);

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

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);

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

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 4);

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

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 2);

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
    public async Task Execute_UpdateOptions_SetsProrationBehaviorToAlwaysInvoice()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);

        _stripeFacade.ListSubscriptionsAutoPagingAsync(Arg.Any<SubscriptionListOptions>())
            .Returns(AsyncEnumerable.Create(subscription));
        _stripeFacade.UpdateSubscription(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);

        // Act
        await _sut.Execute(context);

        // Assert
        await _stripeFacade.Received(1).UpdateSubscription(
            "sub_123",
            Arg.Is<SubscriptionUpdateOptions>(o => o.ProrationBehavior == "always_invoice"));
    }

    [Fact]
    public async Task Execute_UpdateOptions_SetsReconciledMetadata()
    {
        // Arrange
        var context = CreateJobExecutionContext();
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_EnableReconcileAdditionalStorageJob).Returns(true);
        _featureService.IsEnabled(FeatureFlagKeys.PM28265_ReconcileAdditionalStorageJobEnableLiveMode).Returns(true);

        var subscription = CreateSubscription("sub_123", "storage-gb-monthly", quantity: 10);

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

        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);
        var subscription2 = CreateSubscription("sub_2", "storage-gb-monthly", quantity: 5);
        var subscription3 = CreateSubscription("sub_3", "storage-gb-monthly", quantity: 3);

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

        var processedMetadata = new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.StorageReconciled2025] = DateTime.UtcNow.ToString("o")
        };

        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);
        var subscription2 = CreateSubscription("sub_2", "storage-gb-monthly", quantity: 5, metadata: processedMetadata);
        var subscription3 = CreateSubscription("sub_3", "storage-gb-monthly", quantity: 3);

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

        var subscription1 = CreateSubscription("sub_1", "storage-gb-monthly", quantity: 10);
        var subscription2 = CreateSubscription("sub_2", "storage-gb-monthly", quantity: 5);
        var subscription3 = CreateSubscription("sub_3", "storage-gb-monthly", quantity: 3);

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
        Dictionary<string, string>? metadata = null)
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
            Metadata = metadata,
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem> { item }
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
