using System.Text.Json;
using Bit.Core.Billing.Payment.Models;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Models;

public class PaymentMethodTests
{
    [Fact]
    public void Read_ShouldDeserializeTokenizedPaymentMethod_WhenTypeIsTokenized()
    {
        // Arrange
        var json = "{\"type\":\"tokenized_card\",\"token\":\"test-token\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var result = JsonSerializer.Deserialize<PaymentMethod>(json, options);

        // Assert
        Assert.True(result.IsTokenized);
    }

    [Fact]
    public void Read_ShouldDeserializeNonTokenizedPaymentMethod_WhenTypeIsNonTokenized()
    {
        // Arrange
        var json = "{\"type\":\"non_tokenized_accountcredit\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var result = JsonSerializer.Deserialize<PaymentMethod>(json, options);

        // Assert
        Assert.True(result.IsNonTokenized);
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenTypeIsMissing()
    {
        // Arrange
        var json = "{\"cardNumber\":\"1234\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenTypeIsUnknown()
    {
        // Arrange
        var json = "{\"type\":\"unknown_type\",\"data\":\"value\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Fact]
    public void Write_ShouldSerializeTokenizedPaymentMethod()
    {
        // Arrange
        var paymentMethod = new PaymentMethod(new TokenizedPaymentMethod
        {
            Type = TokenizablePaymentMethodType.Card,
            Token = "test_token"
        });
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var json = JsonSerializer.Serialize(paymentMethod, options);

        // Assert
        Assert.Contains("\"type\":\"tokenized_card\"", json);
        Assert.Contains("\"token\":\"test_token\"", json);
    }

    [Fact]
    public void Write_ShouldSerializeNonTokenizedPaymentMethod()
    {
        // Arrange
        var paymentMethod =
            new PaymentMethod(new NonTokenizedPaymentMethod { Type = NonTokenizablePaymentMethodType.AccountCredit });
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act
        var json = JsonSerializer.Serialize(paymentMethod, options);

        // Assert
        Assert.Contains("\"type\":\"non_tokenized_accountcredit\"", json);
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenTokenizedPaymentMethodMissingToken()
    {
        // Arrange
        var json = "{\"type\":\"tokenized_card\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenTokenizedPaymentMethodEmptyToken()
    {
        // Arrange
        var json = "{\"type\":\"tokenized_card\",\"token\":\"\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenTokenizedPaymentMethodContainsNullToken()
    {
        // Arrange
        var json = "{\"type\":\"tokenized_card\",\"token\":null}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenInvalidTokenizedPaymentMethodType()
    {
        // Arrange
        var json = "{\"type\":\"tokenized_invalid\",\"token\":\"test-token\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }

    [Fact]
    public void Read_ShouldThrowJsonException_WhenInvalidNonTokenizedPaymentMethodType()
    {
        // Arrange
        var json = "{\"type\":\"non_tokenized_invalid\"}";
        var options = new JsonSerializerOptions { Converters = { new PaymentMethodJsonConverter() } };

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentMethod>(json, options));
    }
}
