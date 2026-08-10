using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Invoicing.InvoicePreviews.Models;
using Xunit;

namespace Bit.Invoicing.Test.Models;

public class InvoicePreviewSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static InvoicePreview Sample() => new()
    {
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 5, Cost = 35.82m },
        },
        Cadence = PlanCadenceType.Annually,
        PlanTier = PlanTierType.Enterprise,
        EstimatedTax = 2.11m,
        Total = 37.93m,
        AmountDue = 37.93m,
    };

    [Fact]
    public void Serializes_TierAndCadence_AsEnumMemberStrings()
    {
        var json = JsonSerializer.Serialize(Sample(), Options);
        Assert.Contains("\"planTier\":\"enterprise\"", json);
        Assert.Contains("\"cadence\":\"annually\"", json);
    }

    [Fact]
    public void Serializes_DiscountType_AsEnumMemberString()
    {
        var preview = Sample() with
        {
            Discounts = [new InvoicePreviewDiscount { Type = BitwardenDiscountType.PercentOff, Value = 10m, Amount = 3.79m, Label = "LAUNCH" }],
        };
        var json = JsonSerializer.Serialize(preview, Options);
        Assert.Contains("\"type\":\"percent-off\"", json);
    }

    [Fact]
    public void Serializes_NullOptionalFields_AsNull()
    {
        var json = JsonSerializer.Serialize(Sample(), Options);
        Assert.Contains("\"secretsManager\":null", json);
        Assert.Contains("\"discounts\":null", json);
        Assert.Contains("\"startingBalance\":null", json);
    }
}
