using System.Text.Json;
using Bit.Core.Billing.Payment.Models;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Models;

public class PaymentMethodTests
{
    [Theory]
    [InlineData("{\"cardNumber\":\"1234\"}")]
    [InlineData("{\"type\":\"unknown_type\",\"data\":\"value\"}")]
    [InlineData("{\"type\":\"invalid\",\"token\":\"test-token\"}")]
    [InlineData("{\"type\":\"invalid\"}")]
    public void Read_ShouldThrowJsonException_OnInvalidOrMissingType(string json)
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Theory]
    [InlineData("{\"type\":\"card\"}")]
    [InlineData("{\"type\":\"card\",\"token\":\"\"}")]
    [InlineData("{\"type\":\"card\",\"token\":null}")]
    public void Read_ShouldThrowJsonException_OnInvalidTokenizedPaymentMethodToken(string json)
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    // Tokenized payment method deserialization
    [Theory]
    [InlineData("bankAccount", TokenizablePaymentMethodType.BankAccount)]
    [InlineData("card", TokenizablePaymentMethodType.Card)]
    [InlineData("payPal", TokenizablePaymentMethodType.PayPal)]
    public void Read_ShouldDeserializeTokenizedPaymentMethods(string typeString, TokenizablePaymentMethodType expectedType)
    {
        // Arrange
        var json = $"{{\"type\":\"{typeString}\",\"token\":\"test-token\"}}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var result = JsonSerializer.Deserialize<PaymentMethod>(json, options);

        // Assert
        Assert.True(result.IsTokenized);
        Assert.Equal(expectedType, result.AsT0.Type);
        Assert.Equal("test-token", result.AsT0.Token);
    }

    // Non-tokenized payment method deserialization
    [Theory]
    [InlineData("accountcredit", NonTokenizablePaymentMethodType.AccountCredit)]
    public void Read_ShouldDeserializeNonTokenizedPaymentMethods(string typeString, NonTokenizablePaymentMethodType expectedType)
    {
        // Arrange
        var json = $"{{\"type\":\"{typeString}\"}}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var result = JsonSerializer.Deserialize<PaymentMethod>(json, options);

        // Assert
        Assert.True(result.IsNonTokenized);
        Assert.Equal(expectedType, result.AsT1.Type);
    }

    // Tokenized payment method serialization
    [Theory]
    [InlineData(TokenizablePaymentMethodType.BankAccount, "bankaccount")]
    [InlineData(TokenizablePaymentMethodType.Card, "card")]
    [InlineData(TokenizablePaymentMethodType.PayPal, "paypal")]
    public void Write_ShouldSerializeTokenizedPaymentMethods(TokenizablePaymentMethodType type, string expectedTypeString)
    {
        // Arrange
        var paymentMethod = new PaymentMethod(new TokenizedPaymentMethod
        {
            Type = type,
            Token = "test-token"
        });
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var json = JsonSerializer.Serialize(paymentMethod, options);

        // Assert
        Assert.Contains($"\"type\":\"{expectedTypeString}\"", json);
        Assert.Contains("\"token\":\"test-token\"", json);
    }

    // Non-tokenized payment method serialization
    [Theory]
    [InlineData(NonTokenizablePaymentMethodType.AccountCredit, "accountcredit")]
    public void Write_ShouldSerializeNonTokenizedPaymentMethods(NonTokenizablePaymentMethodType type, string expectedTypeString)
    {
        // Arrange
        var paymentMethod = new PaymentMethod(new NonTokenizedPaymentMethod { Type = type });
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var json = JsonSerializer.Serialize(paymentMethod, options);

        // Assert
        Assert.Contains($"\"type\":\"{expectedTypeString}\"", json);
        Assert.DoesNotContain("token", json);
    }
}
