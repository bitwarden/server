using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Business
{
    public class BillingInfo
    {
        public decimal CreditAmount { get; set; }
        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public BillingInvoice UpcomingInvoice { get; set; }
        public IEnumerable<BillingCharge> Charges { get; set; } = new List<BillingCharge>();
        public IEnumerable<BillingInvoice2> Invoices { get; set; } = new List<BillingInvoice2>();
        public IEnumerable<BillingTransaction> Transactions { get; set; } = new List<BillingTransaction>();

        public class BillingSource
        {
            public BillingSource(IPaymentSource source)
            {
                if(source is BankAccount bankAccount)
                {
                    Type = PaymentMethodType.BankAccount;
                    Description = $"{bankAccount.BankName}, *{bankAccount.Last4} - " +
                        (bankAccount.Status == "verified" ? "verified" :
                        bankAccount.Status == "errored" ? "invalid" :
                        bankAccount.Status == "verification_failed" ? "verification failed" : "unverified");
                    NeedsVerification = bankAccount.Status == "new" || bankAccount.Status == "validated";
                }
                else if(source is Card card)
                {
                    Type = PaymentMethodType.Card;
                    Description = $"{card.Brand}, *{card.Last4}, " +
                        string.Format("{0}/{1}",
                            string.Concat(card.ExpMonth < 10 ?
                                "0" : string.Empty, card.ExpMonth),
                            card.ExpYear);
                    CardBrand = card.Brand;
                }
            }

            public BillingSource(Braintree.PaymentMethod method)
            {
                if(method is Braintree.PayPalAccount paypal)
                {
                    Type = PaymentMethodType.PayPal;
                    Description = paypal.Email;
                }
                else if(method is Braintree.CreditCard card)
                {
                    Type = PaymentMethodType.Card;
                    Description = $"{card.CardType.ToString()}, *{card.LastFour}, " +
                        string.Format("{0}/{1}",
                            string.Concat(card.ExpirationMonth.Length == 1 ?
                                "0" : string.Empty, card.ExpirationMonth),
                            card.ExpirationYear);
                    CardBrand = card.CardType.ToString();
                }
                else if(method is Braintree.UsBankAccount bank)
                {
                    Type = PaymentMethodType.BankAccount;
                    Description = $"{bank.BankName}, *{bank.Last4}";
                }
                else
                {
                    throw new NotSupportedException("Method not supported.");
                }
            }

            public BillingSource(Braintree.UsBankAccountDetails bank)
            {
                Type = PaymentMethodType.BankAccount;
                Description = $"{bank.BankName}, *{bank.Last4}";
            }

            public BillingSource(Braintree.PayPalDetails paypal)
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
            public BillingSubscription(Subscription sub)
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

            public BillingSubscription(Braintree.Subscription sub, Braintree.Plan plan)
            {
                Status = sub.Status.ToString();

                if(sub.HasTrialPeriod.GetValueOrDefault() && sub.CreatedAt.HasValue && sub.TrialDuration.HasValue)
                {
                    TrialStartDate = sub.CreatedAt.Value;
                    if(sub.TrialDurationUnit == Braintree.SubscriptionDurationUnit.DAY)
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
                Cancelled = sub.Status == Braintree.SubscriptionStatus.CANCELED;
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
                public BillingSubscriptionItem(SubscriptionItem item)
                {
                    if(item.Plan != null)
                    {
                        Name = item.Plan.Nickname;
                        Amount = item.Plan.Amount.GetValueOrDefault() / 100M;
                        Interval = item.Plan.Interval;
                    }

                    Quantity = (int)item.Quantity;
                }

                public BillingSubscriptionItem(Braintree.Plan plan)
                {
                    Name = plan.Name;
                    Amount = plan.Price.GetValueOrDefault();
                    Interval = plan.BillingFrequency.GetValueOrDefault() == 12 ? "year" : "month";
                    Quantity = 1;
                }

                public BillingSubscriptionItem(Braintree.Plan plan, Braintree.AddOn addon)
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
            public BillingInvoice() { }

            public BillingInvoice(Invoice inv)
            {
                Amount = inv.AmountDue / 100M;
                Date = inv.Date.Value;
            }

            public BillingInvoice(Braintree.Subscription sub)
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
            public BillingCharge(Charge charge)
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

            public BillingCharge(Braintree.Transaction transaction)
            {
                Amount = transaction.Amount.GetValueOrDefault();
                RefundedAmount = 0; // TODO?

                if(transaction.PayPalDetails != null)
                {
                    PaymentSource = new BillingSource(transaction.PayPalDetails);
                }
                else if(transaction.CreditCard != null &&
                    transaction.CreditCard.CardType != Braintree.CreditCardCardType.UNRECOGNIZED)
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

        public class BillingTransaction
        {
            public BillingTransaction(Transaction transaction)
            {
                CreatedDate = transaction.CreationDate;
                Refunded = transaction.Refunded;
                Type = transaction.Type;
                PaymentMethodType = transaction.PaymentMethodType;
                Details = transaction.Details;

                if(transaction.RefundedAmount.HasValue)
                {
                    RefundedAmount = Math.Abs(transaction.RefundedAmount.Value);
                }
                switch(transaction.Type)
                {
                    case TransactionType.Charge:
                    case TransactionType.Credit:
                    case TransactionType.PromotionalCredit:
                    case TransactionType.ReferralCredit:
                        Amount = -1 * Math.Abs(transaction.Amount);
                        break;
                    case TransactionType.Refund:
                        Amount = Math.Abs(transaction.Amount);
                        break;
                    default:
                        break;
                }
            }

            public DateTime CreatedDate { get; set; }
            public decimal Amount { get; set; }
            public bool? Refunded { get; set; }
            public bool? PartiallyRefunded => !Refunded.GetValueOrDefault() && RefundedAmount.GetValueOrDefault() > 0;
            public decimal? RefundedAmount { get; set; }
            public TransactionType Type { get; set; }
            public PaymentMethodType? PaymentMethodType { get; set; }
            public string Details { get; set; }
        }

        public class BillingInvoice2 : BillingInvoice
        {
            public BillingInvoice2(Invoice inv)
            {
                Url = inv.HostedInvoiceUrl;
                PdfUrl = inv.InvoicePdf;
                Number = inv.Number;
                Paid = inv.Paid;
                Amount = inv.Total / 100M;
                Date = inv.Date.Value;
            }

            public string Url { get; set; }
            public string PdfUrl { get; set; }
            public string Number { get; set; }
            public bool Paid { get; set; }
        }
    }
}
