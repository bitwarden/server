using System.Text.Json;
using Bit.Core.Billing.Payment.Models;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Models;

public class MaskedPaymentMethodTests
{
    [Fact]
    public void Write_Read_BankAccount_Succeeds()
    {
        MaskedPaymentMethod input = new MaskedBankAccount
        {
            BankName = "Chase",
            Last4 = "9999",
            HostedVerificationUrl = "https://example.com"
        };

        var json = JsonSerializer.Serialize(input);

        var output = JsonSerializer.Deserialize<MaskedPaymentMethod>(json);
        Assert.NotNull(output);
        Assert.True(output.IsT0);

        Assert.Equivalent(input.AsT0, output.AsT0);
    }

    [Fact]
    public void Write_Read_BankAccount_WithOptions_Succeeds()
    {
        MaskedPaymentMethod input = new MaskedBankAccount
        {
            BankName = "Chase",
            Last4 = "9999",
            HostedVerificationUrl = "https://example.com"
        };

        var jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = JsonSerializer.Serialize(input, jsonSerializerOptions);

        var output = JsonSerializer.Deserialize<MaskedPaymentMethod>(json, jsonSerializerOptions);
        Assert.NotNull(output);
        Assert.True(output.IsT0);

        Assert.Equivalent(input.AsT0, output.AsT0);
    }

    [Fact]
    public void Write_Read_Card_Succeeds()
    {
        MaskedPaymentMethod input = new MaskedCard
        {
            Brand = "visa",
            Last4 = "9999",
            Expiration = "01/2028"
        };

        var json = JsonSerializer.Serialize(input);

        var output = JsonSerializer.Deserialize<MaskedPaymentMethod>(json);
        Assert.NotNull(output);
        Assert.True(output.IsT1);

        Assert.Equivalent(input.AsT1, output.AsT1);
    }

    [Fact]
    public void Write_Read_PayPal_Succeeds()
    {
        MaskedPaymentMethod input = new MaskedPayPalAccount
        {
            Email = "paypal-user@gmail.com"
        };

        var json = JsonSerializer.Serialize(input);

        var output = JsonSerializer.Deserialize<MaskedPaymentMethod>(json);
        Assert.NotNull(output);
        Assert.True(output.IsT2);

        Assert.Equivalent(input.AsT2, output.AsT2);
    }
}
