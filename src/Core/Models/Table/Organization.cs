using System;
using Bit.Core.Utilities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Exceptions;

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
        public GatewayType? Gateway { get; set; }
        public string GatewayCustomerId { get; set; }
        public string GatewaySubscriptionId { get; set; }
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

        public IPaymentService GetPaymentService(GlobalSettings globalSettings)
        {
            if(Gateway == null)
            {
                throw new BadRequestException("No gateway.");
            }

            IPaymentService paymentService = null;
            switch(Gateway)
            {
                case GatewayType.Stripe:
                    paymentService = new StripePaymentService();
                    break;
                case GatewayType.Braintree:
                    paymentService = new BraintreePaymentService(globalSettings);
                    break;
                default:
                    throw new NotSupportedException("Unsupported gateway.");
            }

            return paymentService;
        }
    }
}
