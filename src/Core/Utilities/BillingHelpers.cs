using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.Services;

namespace Bit.Core.Utilities;

public static class BillingHelpers
{
    internal static async Task<string?> AdjustStorageAsync(
        IStripePaymentService paymentService,
        IUpdateOrganizationSubscriptionCommand? updateOrganizationSubscriptionCommand,
        IFeatureService featureService,
        IStorableSubscriber storableSubscriber,
        short storageAdjustmentGb,
        string storagePlanId,
        short baseStorageGb,
        Plan? plan = null)
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
        if (newStorageGb < baseStorageGb)
        {
            newStorageGb = baseStorageGb;
        }

        if (newStorageGb > 100)
        {
            throw new BadRequestException("Maximum storage is 100 GB.");
        }

        var remainingStorage = storableSubscriber.StorageBytesRemaining(newStorageGb);
        if (remainingStorage < 0)
        {
            throw new BadRequestException("You are currently using " +
                $"{CoreHelpers.ReadableBytesSize(storableSubscriber.Storage.GetValueOrDefault(0))} of storage. " +
                "Delete some stored data first.");
        }

        var additionalStorage = newStorageGb - baseStorageGb;

        if (storableSubscriber is Organization organization &&
            updateOrganizationSubscriptionCommand != null &&
            plan != null &&
            featureService.IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand))
        {
            var builder = OrganizationSubscriptionChangeSet.Builder(plan);
            if (organization.MaxStorageGb > plan.PasswordManager.BaseStorageGb)
            {
                builder.UpdateStorage(additionalStorage);
            }
            else
            {
                builder.AddStorage(additionalStorage);
            }

            var changeSet = builder.Build();
            var result = await updateOrganizationSubscriptionCommand.Run(organization, changeSet);
            result.GetValueOrThrow();
            storableSubscriber.MaxStorageGb = newStorageGb;
            return null;
        }

        var paymentIntentClientSecret = await paymentService.AdjustStorageAsync(storableSubscriber,
            additionalStorage, storagePlanId);
        storableSubscriber.MaxStorageGb = newStorageGb;
        return paymentIntentClientSecret;
    }
}
