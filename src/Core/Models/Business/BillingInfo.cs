using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Stripe;
using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Business
{
    public class BillingInfo
    {
        public decimal Balance { get; set; }
        public BillingSource PaymentSource { get; set; }
        public IEnumerable<BillingInvoice> Invoices { get; set; } = new List<BillingInvoice>();
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
                else if(source is Source src && src.Card != null)
                {
                    Type = PaymentMethodType.Card;
                    Description = $"{src.Card.Brand}, *{src.Card.Last4}, " +
                        string.Format("{0}/{1}",
                            string.Concat(src.Card.ExpMonth < 10 ?
                                "0" : string.Empty, src.Card.ExpMonth),
                            src.Card.ExpYear);
                    CardBrand = src.Card.Brand;
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

        public class BillingTransaction
        {
            public BillingTransaction(Transaction transaction)
            {
                Id = transaction.Id;
                CreatedDate = transaction.CreationDate;
                Refunded = transaction.Refunded;
                Type = transaction.Type;
                PaymentMethodType = transaction.PaymentMethodType;
                Details = transaction.Details;
                Amount = transaction.Amount;
                RefundedAmount = transaction.RefundedAmount;
            }

            public Guid Id { get; set; }
            public DateTime CreatedDate { get; set; }
            public decimal Amount { get; set; }
            public bool? Refunded { get; set; }
            public bool? PartiallyRefunded => !Refunded.GetValueOrDefault() && RefundedAmount.GetValueOrDefault() > 0;
            public decimal? RefundedAmount { get; set; }
            public TransactionType Type { get; set; }
            public PaymentMethodType? PaymentMethodType { get; set; }
            public string Details { get; set; }
        }

        public class BillingInvoice
        {
            public BillingInvoice(Invoice inv)
            {
                Amount = inv.AmountDue / 100M;
                Date = inv.Date.Value;
                Url = inv.HostedInvoiceUrl;
                PdfUrl = inv.InvoicePdf;
                Number = inv.Number;
                Paid = inv.Paid;
                Amount = inv.Total / 100M;
                Date = inv.Date.Value;
            }

            public decimal Amount { get; set; }
            public DateTime? Date { get; set; }
            public string Url { get; set; }
            public string PdfUrl { get; set; }
            public string Number { get; set; }
            public bool Paid { get; set; }
        }
    }
}
