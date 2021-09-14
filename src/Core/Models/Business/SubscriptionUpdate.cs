using System.Linq;
using Bit.Core.Models.Table;
using Stripe;
using StaticStore = Bit.Core.Models.StaticStore;

namespace Bit.Core.Models.Business
{
    public abstract class SubscriptionUpdate
    {
        protected abstract string PlanId { get; }

        public abstract SubscriptionItemOptions RevertItemOptions(Subscription subscription);
        public abstract SubscriptionItemOptions UpgradeItemOptions(Subscription subscription);
        protected SubscriptionItem SubscriptionItem(Subscription subscription) =>
            subscription.Items?.Data?.FirstOrDefault(i => i.Plan.Id == PlanId);
    }


    public class SeatSubscriptionUpdate : SubscriptionUpdate
    {
        private readonly Organization _organization;
        private readonly StaticStore.Plan _plan;
        private readonly long? _additionalSeats;
        protected override string PlanId => _plan.StripeSeatPlanId;

        public SeatSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats)
        {
            _organization = organization;
            _plan = plan;
            _additionalSeats = additionalSeats;
        }

        public override SubscriptionItemOptions UpgradeItemOptions(Subscription subscription)
        {
            var item = SubscriptionItem(subscription);
            return new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = PlanId,
                Quantity = _additionalSeats,
                Deleted = (item?.Id != null && _additionalSeats == 0) ? true : (bool?)null,
            };
        }

        public override SubscriptionItemOptions RevertItemOptions(Subscription subscription)
        {
            var item = SubscriptionItem(subscription);
            return new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = PlanId,
                Quantity = _organization.Seats,
                Deleted = item?.Id != null ? true : (bool?)null,
            };
        }
    }

    public class StorageSubscriptionUpdate : SubscriptionUpdate
    {
        private readonly string _plan;
        private readonly long? _additionalStorage;
        protected override string PlanId => _plan;

        public StorageSubscriptionUpdate(string plan, long? additionalStorage)
        {
            _plan = plan;
            _additionalStorage = additionalStorage;
        }

        public override SubscriptionItemOptions UpgradeItemOptions(Subscription subscription)
        {
            var item = SubscriptionItem(subscription);
            return new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = _plan,
                Quantity = _additionalStorage,
                Deleted = (item?.Id != null && _additionalStorage == 0) ? true : (bool?)null,
            };
        }

        public override SubscriptionItemOptions RevertItemOptions(Subscription subscription)
        {
            var item = SubscriptionItem(subscription);
            return new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = _plan,
                Quantity = item?.Quantity ?? 0,
                Deleted = item?.Id != null ? true : (bool?)null,
            };
        }
    }
}
