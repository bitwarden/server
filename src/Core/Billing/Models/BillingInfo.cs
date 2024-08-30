using Bit.Core.Enums;
using Stripe;

namespace Bit.Core.Billing.Models;

public class BillingInfo
{
    public decimal Balance { get; set; }
    public BillingSource PaymentSource { get; set; }

    public class BillingSource
    {
        public BillingSource() { }

        public BillingSource(Stripe.PaymentMethod method)
        {
            if (method.Card == null)
            {
                return;
            }

            Type = PaymentMethodType.Card;
            var card = method.Card;
            Description = $"{card.Brand?.ToUpperInvariant()}, *{card.Last4}, {card.ExpMonth:00}/{card.ExpYear}";
            CardBrand = card.Brand;
        }

        public BillingSource(IPaymentSource source)
        {
            switch (source)
            {
                case BankAccount bankAccount:
                    var bankStatus = bankAccount.Status switch
                    {
                        "verified" => "verified",
                        "errored" => "invalid",
                        "verification_failed" => "verification failed",
                        _ => "unverified"
                    };
                    Type = PaymentMethodType.BankAccount;
                    Description = $"{bankAccount.BankName}, *{bankAccount.Last4} - {bankStatus}";
                    NeedsVerification = bankAccount.Status is "new" or "validated";
                    break;
                case Card card:
                    Type = PaymentMethodType.Card;
                    Description = $"{card.Brand}, *{card.Last4}, {card.ExpMonth:00}/{card.ExpYear}";
                    CardBrand = card.Brand;
                    break;
                case Source { Card: not null } src:
                    Type = PaymentMethodType.Card;
                    Description = $"{src.Card.Brand}, *{src.Card.Last4}, {src.Card.ExpMonth:00}/{src.Card.ExpYear}";
                    CardBrand = src.Card.Brand;
                    break;
            }
        }

        public BillingSource(Braintree.PaymentMethod method)
        {
            switch (method)
            {
                case Braintree.PayPalAccount paypal:
                    Type = PaymentMethodType.PayPal;
                    Description = paypal.Email;
                    break;
                case Braintree.CreditCard card:
                    Type = PaymentMethodType.Card;
                    Description = $"{card.CardType.ToString()}, *{card.LastFour}, " +
                                  $"{card.ExpirationMonth.PadLeft(2, '0')}/{card.ExpirationYear}";
                    CardBrand = card.CardType.ToString();
                    break;
                case Braintree.UsBankAccount bank:
                    Type = PaymentMethodType.BankAccount;
                    Description = $"{bank.BankName}, *{bank.Last4}";
                    break;
                default:
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
}
