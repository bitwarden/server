using Bit.Core.Billing.Constants;
using Bit.Core.Exceptions;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Models;

using static StripeConstants;

public record UserId(Guid Value);

public record OrganizationId(Guid Value);

public record ProviderId(Guid Value);

public class SubscriberId : OneOfBase<UserId, OrganizationId, ProviderId>
{
    private SubscriberId(OneOf<UserId, OrganizationId, ProviderId> input) : base(input) { }

    public static implicit operator SubscriberId(UserId value) => new(value);
    public static implicit operator SubscriberId(OrganizationId value) => new(value);
    public static implicit operator SubscriberId(ProviderId value) => new(value);

    public static implicit operator SubscriberId(Subscription subscription)
    {
        if (subscription.Metadata.TryGetValue(MetadataKeys.UserId, out var userIdValue)
            && Guid.TryParse(userIdValue, out var userId))
        {
            return new UserId(userId);
        }

        if (subscription.Metadata.TryGetValue(MetadataKeys.OrganizationId, out var organizationIdValue)
            && Guid.TryParse(organizationIdValue, out var organizationId))
        {
            return new OrganizationId(organizationId);
        }

        return subscription.Metadata.TryGetValue(MetadataKeys.ProviderId, out var providerIdValue) &&
               Guid.TryParse(providerIdValue, out var providerId)
            ? new ProviderId(providerId)
            : throw new ConflictException("Subscription does not have a valid subscriber ID");
    }
}
