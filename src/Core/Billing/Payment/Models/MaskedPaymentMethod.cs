#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Pricing.JSON;
using Braintree;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Payment.Models;

public record MaskedBankAccount
{
    public required string BankName { get; init; }
    public required string Last4 { get; init; }
    public required bool Verified { get; init; }
    public string Type => "bankAccount";
}

public record MaskedCard
{
    public required string Brand { get; init; }
    public required string Last4 { get; init; }
    public required string Expiration { get; init; }
    public string Type => "card";
}

public record MaskedPayPalAccount
{
    public required string Email { get; init; }
    public string Type => "payPal";
}

[JsonConverter(typeof(MaskedPaymentMethodJsonConverter))]
public class MaskedPaymentMethod(OneOf<MaskedBankAccount, MaskedCard, MaskedPayPalAccount> input)
    : OneOfBase<MaskedBankAccount, MaskedCard, MaskedPayPalAccount>(input)
{
    public static implicit operator MaskedPaymentMethod(MaskedBankAccount bankAccount) => new(bankAccount);
    public static implicit operator MaskedPaymentMethod(MaskedCard card) => new(card);
    public static implicit operator MaskedPaymentMethod(MaskedPayPalAccount payPal) => new(payPal);

    public static MaskedPaymentMethod From(BankAccount bankAccount) => new MaskedBankAccount
    {
        BankName = bankAccount.BankName,
        Last4 = bankAccount.Last4,
        Verified = bankAccount.Status == "verified"
    };

    public static MaskedPaymentMethod From(Card card) => new MaskedCard
    {
        Brand = card.Brand.ToLower(),
        Last4 = card.Last4,
        Expiration = $"{card.ExpMonth:00}/{card.ExpYear}"
    };

    public static MaskedPaymentMethod From(PaymentMethodCard card) => new MaskedCard
    {
        Brand = card.Brand.ToLower(),
        Last4 = card.Last4,
        Expiration = $"{card.ExpMonth:00}/{card.ExpYear}"
    };

    public static MaskedPaymentMethod From(SetupIntent setupIntent) => new MaskedBankAccount
    {
        BankName = setupIntent.PaymentMethod.UsBankAccount.BankName,
        Last4 = setupIntent.PaymentMethod.UsBankAccount.Last4,
        Verified = false
    };

    public static MaskedPaymentMethod From(SourceCard sourceCard) => new MaskedCard
    {
        Brand = sourceCard.Brand.ToLower(),
        Last4 = sourceCard.Last4,
        Expiration = $"{sourceCard.ExpMonth:00}/{sourceCard.ExpYear}"
    };

    public static MaskedPaymentMethod From(PaymentMethodUsBankAccount bankAccount) => new MaskedBankAccount
    {
        BankName = bankAccount.BankName,
        Last4 = bankAccount.Last4,
        Verified = true
    };

    public static MaskedPaymentMethod From(PayPalAccount payPalAccount) => new MaskedPayPalAccount { Email = payPalAccount.Email };
}

public class MaskedPaymentMethodJsonConverter : TypeReadingJsonConverter<MaskedPaymentMethod>
{
    protected override string TypePropertyName => nameof(MaskedBankAccount.Type).ToLower();

    public override MaskedPaymentMethod? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = ReadType(reader);

        return type switch
        {
            "bankAccount" => JsonSerializer.Deserialize<MaskedBankAccount>(ref reader, options) switch
            {
                null => null,
                var bankAccount => new MaskedPaymentMethod(bankAccount)
            },
            "card" => JsonSerializer.Deserialize<MaskedCard>(ref reader, options) switch
            {
                null => null,
                var card => new MaskedPaymentMethod(card)
            },
            "payPal" => JsonSerializer.Deserialize<MaskedPayPalAccount>(ref reader, options) switch
            {
                null => null,
                var payPal => new MaskedPaymentMethod(payPal)
            },
            _ => Skip(ref reader)
        };
    }

    public override void Write(Utf8JsonWriter writer, MaskedPaymentMethod value, JsonSerializerOptions options)
        => value.Switch(
            bankAccount => JsonSerializer.Serialize(writer, bankAccount, options),
            card => JsonSerializer.Serialize(writer, card, options),
            payPal => JsonSerializer.Serialize(writer, payPal, options));
}
