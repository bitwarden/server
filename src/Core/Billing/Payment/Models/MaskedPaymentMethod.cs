using System.Text.Json;
using System.Text.Json.Serialization;
using Braintree;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Payment.Models;

public record MaskedBankAccount
{
    public required string BankName { get; init; }
    public required string Last4 { get; init; }
    public string? HostedVerificationUrl { get; init; }
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
        Last4 = bankAccount.Last4
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
        HostedVerificationUrl = setupIntent.NextAction?.VerifyWithMicrodeposits?.HostedVerificationUrl
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
        Last4 = bankAccount.Last4
    };

    public static MaskedPaymentMethod From(PayPalAccount payPalAccount) => new MaskedPayPalAccount { Email = payPalAccount.Email };
}

public class MaskedPaymentMethodJsonConverter : JsonConverter<MaskedPaymentMethod>
{
    private const string _typePropertyName = nameof(MaskedBankAccount.Type);

    public override MaskedPaymentMethod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);

        if (!element.TryGetProperty(options.PropertyNamingPolicy?.ConvertName(_typePropertyName) ?? _typePropertyName, out var typeProperty))
        {
            throw new JsonException(
                $"Failed to deserialize {nameof(MaskedPaymentMethod)}: missing '{_typePropertyName}' property");
        }

        var type = typeProperty.GetString();

        return type switch
        {
            "bankAccount" => element.Deserialize<MaskedBankAccount>(options)!,
            "card" => element.Deserialize<MaskedCard>(options)!,
            "payPal" => element.Deserialize<MaskedPayPalAccount>(options)!,
            _ => throw new JsonException($"Failed to deserialize {nameof(MaskedPaymentMethod)}: invalid '{_typePropertyName}' value - '{type}'")
        };
    }

    public override void Write(Utf8JsonWriter writer, MaskedPaymentMethod value, JsonSerializerOptions options)
        => value.Switch(
            bankAccount => JsonSerializer.Serialize(writer, bankAccount, options),
            card => JsonSerializer.Serialize(writer, card, options),
            payPal => JsonSerializer.Serialize(writer, payPal, options));
}
