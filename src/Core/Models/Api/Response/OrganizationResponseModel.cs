using System;
using System.Linq;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Business;
using Stripe;

namespace Bit.Core.Models.Api
{
    public class OrganizationResponseModel : ResponseModel
    {
        public OrganizationResponseModel(Organization organization, string obj = "organization")
            : base(obj)
        {
            if(organization == null)
            {
                throw new ArgumentNullException(nameof(organization));
            }

            Id = organization.Id.ToString();
            Name = organization.Name;
            BusinessName = organization.BusinessName;
            BillingEmail = organization.BillingEmail;
            Plan = organization.Plan;
            PlanType = organization.PlanType;
            Seats = organization.Seats;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string BusinessName { get; set; }
        public string BillingEmail { get; set; }
        public string Plan { get; set; }
        public Enums.PlanType PlanType { get; set; }
        public short? Seats { get; set; }
    }

    public class OrganizationBillingResponseModel : OrganizationResponseModel
    {
        public OrganizationBillingResponseModel(Organization organization, OrganizationBilling billing)
            : base(organization, "organizationBilling")
        {
            PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
            Subscription = billing.Subscription != null ? new BillingSubscription(billing.Subscription) : null;
            Charges = billing.Charges.Select(c => new BillingCharge(c));
        }

        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public IEnumerable<BillingCharge> Charges { get; set; }

        public class BillingSource
        {
            public BillingSource(Source source)
            {
                Type = source.Type;

                switch(source.Type)
                {
                    case SourceType.Card:
                        Description = $"{source.Card.Brand}, *{source.Card.Last4}, " +
                            string.Format("{0}/{1}",
                                string.Concat(source.Card.ExpirationMonth.Length == 1 ?
                                    "0" : string.Empty, source.Card.ExpirationMonth),
                                source.Card.ExpirationYear);
                        CardBrand = source.Card.Brand;
                        break;
                    case SourceType.BankAccount:
                        Description = $"{source.BankAccount.BankName}, *{source.BankAccount.Last4}";
                        break;
                    // bitcoin/alipay?
                    default:
                        break;
                }
            }

            public SourceType Type { get; set; }
            public string CardBrand { get; set; }
            public string Description { get; set; }
        }

        public class BillingSubscription
        {
            public BillingSubscription(StripeSubscription sub)
            {
                Status = sub.Status;
                TrialStartDate = sub.TrialStart;
                TrialEndDate = sub.TrialEnd;
                NextBillDate = sub.CurrentPeriodEnd;
                CancelledDate = sub.CanceledAt;
                CancelAtNextBillDate = sub.CancelAtPeriodEnd;
                if(sub.Items?.Data != null)
                {
                    Items = sub.Items.Data.Select(i => new BillingSubscriptionItem(i));
                }
            }

            public DateTime? TrialStartDate { get; set; }
            public DateTime? TrialEndDate { get; set; }
            public DateTime? NextBillDate { get; set; }
            public DateTime? CancelledDate { get; set; }
            public bool CancelAtNextBillDate { get; set; }
            public string Status { get; set; }
            public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();

            public class BillingSubscriptionItem
            {
                public BillingSubscriptionItem(StripeSubscriptionItem item)
                {
                    if(item.Plan != null)
                    {
                        Name = item.Plan.Name;
                        Amount = item.Plan.Amount / 100;
                        Interval = item.Plan.Interval;
                    }

                    Quantity = item.Quantity;
                }

                public string Name { get; set; }
                public decimal Amount { get; set; }
                public int Quantity { get; set; }
                public string Interval { get; set; }
            }
        }

        public class BillingCharge
        {
            public BillingCharge(StripeCharge charge)
            {
                Amount = charge.Amount / 100;
                RefundedAmount = charge.AmountRefunded / 100;
                PaymentSource = charge.Source != null ? new BillingSource(charge.Source) : null;
                CreatedDate = charge.Created;
                FailureMessage = charge.FailureMessage;
                Refunded = charge.Refunded;
                Status = charge.Status;
            }

            public DateTime CreatedDate { get; set; }
            public decimal Amount { get; set; }
            public BillingSource PaymentSource { get; set; }
            public string Status { get; set; }
            public string FailureMessage { get; set; }
            public bool Refunded { get; set; }
            public bool PartiallyRefunded => !Refunded && RefundedAmount > 0;
            public decimal RefundedAmount { get; set; }
        }
    }
}
