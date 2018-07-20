using Bit.Core.Enums;
using Braintree;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Business
{
    public class BillingInfo
    {
        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public BillingInvoice UpcomingInvoice { get; set; }
        public IEnumerable<BillingCharge> Charges { get; set; } = new List<BillingCharge>();

        public class BillingSource
        {
            public BillingSource(Source source)
            {
                switch(source.Type)
                {
                    case SourceType.Card:
                        Type = PaymentMethodType.Card;
                        Description = $"{source.Card.Brand}, *{source.Card.Last4}, " +
                            string.Format("{0}/{1}",
                                string.Concat(source.Card.ExpirationMonth < 10 ?
                                    "0" : string.Empty, source.Card.ExpirationMonth),
                                source.Card.ExpirationYear);
                        CardBrand = source.Card.Brand;
                        break;
                    case SourceType.BankAccount:
                        Type = PaymentMethodType.BankAccount;
                        Description = $"{source.BankAccount.BankName}, *{source.BankAccount.Last4} - " +
                            (source.BankAccount.Status == "verified" ? "verified" :
                            source.BankAccount.Status == "errored" ? "invalid" :
                            source.BankAccount.Status == "verification_failed" ? "verification failed" : "unverified");
                        NeedsVerification = source.BankAccount.Status == "new" || source.BankAccount.Status == "validated";
                        break;
                    default:
                        break;
                }
            }

            public BillingSource(PaymentMethod method)
            {
                if(method is PayPalAccount paypal)
                {
                    Type = PaymentMethodType.PayPal;
                    Description = paypal.Email;
                }
                else if(method is CreditCard card)
                {
                    Type = PaymentMethodType.Card;
                    Description = $"{card.CardType.ToString()}, *{card.LastFour}, " +
                        string.Format("{0}/{1}",
                            string.Concat(card.ExpirationMonth.Length == 1 ?
                                "0" : string.Empty, card.ExpirationMonth),
                            card.ExpirationYear);
                    CardBrand = card.CardType.ToString();
                }
                else if(method is UsBankAccount bank)
                {
                    Type = PaymentMethodType.BankAccount;
                    Description = $"{bank.BankName}, *{bank.Last4}";
                }
                else
                {
                    throw new NotSupportedException("Method not supported.");
                }
            }

            public BillingSource(UsBankAccountDetails bank)
            {
                Type = PaymentMethodType.BankAccount;
                Description = $"{bank.BankName}, *{bank.Last4}";
            }

            public BillingSource(PayPalDetails paypal)
            {
                Type = PaymentMethodType.PayPal;
                Description = paypal.PayerEmail;
            }

            public PaymentMethodType Type { get; set; }
            public string CardBrand { get; set; }
            public string Description { get; set; }
            public bool NeedsVerification { get; set; }
        }

        public class BillingSubscription
        {
            public BillingSubscription(StripeSubscription sub)
            {
                Status = sub.Status;
                TrialStartDate = sub.TrialStart;
                TrialEndDate = sub.TrialEnd;
                PeriodStartDate = sub.CurrentPeriodStart;
                PeriodEndDate = sub.CurrentPeriodEnd;
                CancelledDate = sub.CanceledAt;
                CancelAtEndDate = sub.CancelAtPeriodEnd;
                Cancelled = sub.Status == "canceled" || sub.Status == "unpaid";
                if(sub.Items?.Data != null)
                {
                    Items = sub.Items.Data.Select(i => new BillingSubscriptionItem(i));
                }
            }

            public BillingSubscription(Subscription sub, Plan plan)
            {
                Status = sub.Status.ToString();

                if(sub.HasTrialPeriod.GetValueOrDefault() && sub.CreatedAt.HasValue && sub.TrialDuration.HasValue)
                {
                    TrialStartDate = sub.CreatedAt.Value;
                    if(sub.TrialDurationUnit == SubscriptionDurationUnit.DAY)
                    {
                        TrialEndDate = TrialStartDate.Value.AddDays(sub.TrialDuration.Value);
                    }
                    else
                    {
                        TrialEndDate = TrialStartDate.Value.AddMonths(sub.TrialDuration.Value);
                    }
                }

                PeriodStartDate = sub.BillingPeriodStartDate;
                PeriodEndDate = sub.BillingPeriodEndDate;

                CancelAtEndDate = !sub.NeverExpires.GetValueOrDefault();
                Cancelled = sub.Status == SubscriptionStatus.CANCELED;
                if(Cancelled)
                {
                    CancelledDate = sub.UpdatedAt.Value;
                }

                var items = new List<BillingSubscriptionItem>();
                items.Add(new BillingSubscriptionItem(plan));
                if(sub.AddOns != null)
                {
                    items.AddRange(sub.AddOns.Select(a => new BillingSubscriptionItem(plan, a)));
                }

                if(items.Count > 0)
                {
                    Items = items;
                }
            }

            public DateTime? TrialStartDate { get; set; }
            public DateTime? TrialEndDate { get; set; }
            public DateTime? PeriodStartDate { get; set; }
            public DateTime? PeriodEndDate { get; set; }
            public TimeSpan? PeriodDuration => PeriodEndDate - PeriodStartDate;
            public DateTime? CancelledDate { get; set; }
            public bool CancelAtEndDate { get; set; }
            public string Status { get; set; }
            public bool Cancelled { get; set; }
            public IEnumerable<BillingSubscriptionItem> Items { get; set; } = new List<BillingSubscriptionItem>();

            public class BillingSubscriptionItem
            {
                public BillingSubscriptionItem(StripeSubscriptionItem item)
                {
                    if(item.Plan != null)
                    {
                        Name = item.Plan.Nickname;
                        Amount = item.Plan.Amount.GetValueOrDefault() / 100M;
                        Interval = item.Plan.Interval;
                    }

                    Quantity = item.Quantity;
                }

                public BillingSubscriptionItem(Plan plan)
                {
                    Name = plan.Name;
                    Amount = plan.Price.GetValueOrDefault();
                    Interval = plan.BillingFrequency.GetValueOrDefault() == 12 ? "year" : "month";
                    Quantity = 1;
                }

                public BillingSubscriptionItem(Plan plan, AddOn addon)
                {
                    Name = addon.Name;
                    Amount = addon.Amount.GetValueOrDefault();
                    Interval = plan.BillingFrequency.GetValueOrDefault() == 12 ? "year" : "month";
                    Quantity = addon.Quantity.GetValueOrDefault();
                }

                public string Name { get; set; }
                public decimal Amount { get; set; }
                public int Quantity { get; set; }
                public string Interval { get; set; }
            }
        }

        public class BillingInvoice
        {
            public BillingInvoice(StripeInvoice inv)
            {
                Amount = inv.AmountDue / 100M;
                Date = inv.Date.Value;
            }

            public BillingInvoice(Subscription sub)
            {
                Amount = sub.NextBillAmount.GetValueOrDefault() + sub.Balance.GetValueOrDefault();
                if(Amount < 0)
                {
                    Amount = 0;
                }
                Date = sub.NextBillingDate;
            }

            public decimal Amount { get; set; }
            public DateTime? Date { get; set; }
        }

        public class BillingCharge
        {
            public BillingCharge(StripeCharge charge)
            {
                Amount = charge.Amount / 100M;
                RefundedAmount = charge.AmountRefunded / 100M;
                PaymentSource = charge.Source != null ? new BillingSource(charge.Source) : null;
                CreatedDate = charge.Created;
                FailureMessage = charge.FailureMessage;
                Refunded = charge.Refunded;
                Status = charge.Status;
                InvoiceId = charge.InvoiceId;
            }

            public BillingCharge(Transaction transaction)
            {
                Amount = transaction.Amount.GetValueOrDefault();
                RefundedAmount = 0; // TODO?

                if(transaction.PayPalDetails != null)
                {
                    PaymentSource = new BillingSource(transaction.PayPalDetails);
                }
                else if(transaction.CreditCard != null && transaction.CreditCard.CardType != CreditCardCardType.UNRECOGNIZED)
                {
                    PaymentSource = new BillingSource(transaction.CreditCard);
                }
                else if(transaction.UsBankAccountDetails != null)
                {
                    PaymentSource = new BillingSource(transaction.UsBankAccountDetails);
                }

                CreatedDate = transaction.CreatedAt.GetValueOrDefault();
                FailureMessage = null;
                Refunded = transaction.RefundedTransactionId != null;
                Status = transaction.Status.ToString();
                InvoiceId = null;
            }

            public DateTime CreatedDate { get; set; }
            public decimal Amount { get; set; }
            public BillingSource PaymentSource { get; set; }
            public string Status { get; set; }
            public string FailureMessage { get; set; }
            public bool Refunded { get; set; }
            public bool PartiallyRefunded => !Refunded && RefundedAmount > 0;
            public decimal RefundedAmount { get; set; }
            public string InvoiceId { get; set; }
        }
    }
}
