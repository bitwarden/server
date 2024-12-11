using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.Utilities;

public static class BillingHelpers
{
    internal static async Task<string> AdjustStorageAsync(
        IPaymentService paymentService,
        IStorableSubscriber storableSubscriber,
        short storageAdjustmentGb,
        string storagePlanId
    )
    {
        if (storableSubscriber == null)
        {
            throw new ArgumentNullException(nameof(storableSubscriber));
        }

        if (string.IsNullOrWhiteSpace(storableSubscriber.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(storableSubscriber.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        if (!storableSubscriber.MaxStorageGb.HasValue)
        {
            throw new BadRequestException("No access to storage.");
        }

        var newStorageGb = (short)(storableSubscriber.MaxStorageGb.Value + storageAdjustmentGb);
        if (newStorageGb < 1)
        {
            newStorageGb = 1;
        }

        if (newStorageGb > 100)
        {
            throw new BadRequestException("Maximum storage is 100 GB.");
        }

        var remainingStorage = storableSubscriber.StorageBytesRemaining(newStorageGb);
        if (remainingStorage < 0)
        {
            throw new BadRequestException(
                "You are currently using "
                    + $"{CoreHelpers.ReadableBytesSize(storableSubscriber.Storage.GetValueOrDefault(0))} of storage. "
                    + "Delete some stored data first."
            );
        }

        var additionalStorage = newStorageGb - 1;
        var paymentIntentClientSecret = await paymentService.AdjustStorageAsync(
            storableSubscriber,
            additionalStorage,
            storagePlanId
        );
        storableSubscriber.MaxStorageGb = newStorageGb;
        return paymentIntentClientSecret;
    }
}
