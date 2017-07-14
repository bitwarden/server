using System;
using Bit.Core.Utilities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Table
{
    public class Organization : ITableObject<Guid>, ISubscriber, IStorable, IStorableSubscriber, IRevisable
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string BusinessName { get; set; }
        public string BillingEmail { get; set; }
        public string Plan { get; set; }
        public PlanType PlanType { get; set; }
        public short? Seats { get; set; }
        public short? MaxCollections { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseTotp { get; set; }
        public long? Storage { get; set; }
        public short? MaxStorageGb { get; set; }
        public string StripeCustomerId { get; set; }
        public string StripeSubscriptionId { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

        public void SetNewId()
        {
            if(Id == default(Guid))
            {
                Id = CoreHelpers.GenerateComb();
            }
        }

        public string BillingEmailAddress()
        {
            return BillingEmail;
        }

        public string BillingName()
        {
            return BusinessName;
        }

        public long StorageBytesRemaining()
        {
            if(!MaxStorageGb.HasValue)
            {
                return 0;
            }

            return StorageBytesRemaining(MaxStorageGb.Value);
        }

        public long StorageBytesRemaining(short maxStorageGb)
        {
            var maxStorageBytes = maxStorageGb * 1073741824L;
            if(!Storage.HasValue)
            {
                return maxStorageBytes;
            }

            return maxStorageBytes - Storage.Value;
        }
    }
}
