using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record PaymentMethodDTO(
    PaymentMethodType Type,
    string Description,
    bool NeedsVerification)
{
    public static PaymentMethodDTO From(Stripe.Customer customer)
    {
        var defaultPaymentMethod = customer.InvoiceSettings?.DefaultPaymentMethod;

        if (defaultPaymentMethod == null)
        {
            return customer.DefaultSource != null ? FromStripeLegacyPaymentSource(customer.DefaultSource) : null;
        }

        return defaultPaymentMethod.Type switch
        {
            "card" => FromStripeCardPaymentMethod(defaultPaymentMethod.Card),
            "us_bank_account" => FromStripeBankAccountPaymentMethod(defaultPaymentMethod.UsBankAccount),
            _ => null
        };
    }

    public static PaymentMethodDTO From(Stripe.SetupIntent setupIntent)
    {
        if (!setupIntent.IsUnverifiedBankAccount())
        {
            return null;
        }

        var bankAccount = setupIntent.PaymentMethod.UsBankAccount;

        var description = $"{bankAccount.BankName}, *{bankAccount.Last4}";

        return new PaymentMethodDTO(
            PaymentMethodType.BankAccount,
            description,
            true);
    }

    public static PaymentMethodDTO From(Braintree.Customer customer)
    {
        var defaultPaymentMethod = customer.DefaultPaymentMethod;

        if (defaultPaymentMethod == null)
        {
            return null;
        }

        switch (defaultPaymentMethod)
        {
            case Braintree.PayPalAccount payPalAccount:
                {
                    return new PaymentMethodDTO(
                        PaymentMethodType.PayPal,
                        payPalAccount.Email,
                        false);
                }
            case Braintree.CreditCard creditCard:
                {
                    var formattedExpirationMonth =
                        string.Concat(creditCard.ExpirationMonth.Length == 1 ? "0" : string.Empty,
                            creditCard.ExpirationMonth);

                    var description =
                        $"{creditCard.CardType}, *{creditCard.LastFour}, {formattedExpirationMonth}/{creditCard.ExpirationYear}";

                    return new PaymentMethodDTO(
                        PaymentMethodType.Card,
                        description,
                        false);
                }
            case Braintree.UsBankAccount bankAccount:
                {
                    return new PaymentMethodDTO(
                        PaymentMethodType.BankAccount,
                        $"{bankAccount.BankName}, *{bankAccount.Last4}",
                        false);
                }
            default:
                {
                    return null;
                }
        }
    }

    private static PaymentMethodDTO FromStripeBankAccountPaymentMethod(Stripe.PaymentMethodUsBankAccount bankAccount)
    {
        var description = $"{bankAccount.BankName}, *{bankAccount.Last4}";

        return new PaymentMethodDTO(
            PaymentMethodType.BankAccount,
            description,
            false);
    }

    private static PaymentMethodDTO FromStripeCardPaymentMethod(Stripe.PaymentMethodCard card)
        => new(
            PaymentMethodType.Card,
            GetCardDescription(card.Brand, card.Last4, card.ExpMonth, card.ExpYear),
            false);

    #region Legacy Source Payments
    private static PaymentMethodDTO FromStripeLegacyPaymentSource(Stripe.IPaymentSource paymentSource)
        => paymentSource switch
        {
            Stripe.BankAccount bankAccount => FromStripeBankAccountLegacySource(bankAccount),
            Stripe.Card card => FromStripeCardLegacySource(card),
            Stripe.Source { Card: not null } source => FromStripeSourceCardLegacySource(source.Card),
            _ => null
        };

    private static PaymentMethodDTO FromStripeBankAccountLegacySource(Stripe.BankAccount bankAccount)
    {
        var status = bankAccount.Status switch
        {
            "verified" => "Verified",
            "errored" => "Invalid",
            "verification_failed" => "Verification failed",
            _ => "Unverified"
        };

        var description = $"{bankAccount.BankName}, *{bankAccount.Last4} - {status}";

        var needsVerification = bankAccount.Status is "new" or "validated";

        return new PaymentMethodDTO(
            PaymentMethodType.BankAccount,
            description,
            needsVerification);
    }

    private static PaymentMethodDTO FromStripeCardLegacySource(Stripe.Card card)
        => new(
            PaymentMethodType.Card,
            GetCardDescription(card.Brand, card.Last4, card.ExpMonth, card.ExpYear),
            false);

    private static PaymentMethodDTO FromStripeSourceCardLegacySource(Stripe.SourceCard card)
        => new(
            PaymentMethodType.Card,
            GetCardDescription(card.Brand, card.Last4, card.ExpMonth, card.ExpYear),
            false);
    #endregion

    private static string GetCardDescription(
        string brand,
        string last4,
        long expirationMonth,
        long expirationYear)
    {
        var formattedExpirationMonth = string.Concat(expirationMonth < 10 ? "0" : string.Empty, expirationMonth);

        return $"{brand.ToUpperInvariant()}, *{last4}, {formattedExpirationMonth}/{expirationYear}";
    }
}
