using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Subscriptions.User.Models.Requests;
using Xunit;

namespace Bit.Subscriptions.User.Test.Models.Requests;

public class PreviewPremiumUpgradeRequestTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("2", ProductTierType.Teams)]
    [InlineData("\"Teams\"", ProductTierType.Teams)]
    [InlineData("\"enterprise\"", ProductTierType.Enterprise)]
    public void Deserialize_AcceptsNumericAndStringTiers(string tierJson, ProductTierType expected)
    {
        var json = $$$"""{"targetProductTierType":{{{tierJson}}},"billingAddress":{"country":"US","postalCode":"12345"}}""";

        var request = JsonSerializer.Deserialize<PreviewPremiumUpgradeRequest>(json, Options);

        Assert.NotNull(request);
        Assert.Equal(expected, request!.TargetProductTierType);
        Assert.Equal("US", request.BillingAddress.Country);
        Assert.Equal("12345", request.BillingAddress.PostalCode);
    }

    [Fact]
    public void Deserialize_ExplicitNullBillingAddress_BindsNull()
    {
        const string json = """{"targetProductTierType":2,"billingAddress":null}""";

        var request = JsonSerializer.Deserialize<PreviewPremiumUpgradeRequest>(json, Options);

        Assert.NotNull(request);
        Assert.Null(request!.BillingAddress);
    }

    [Fact]
    public void Deserialize_MissingBillingAddress_Throws()
    {
        const string json = """{"targetProductTierType":2}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PreviewPremiumUpgradeRequest>(json, Options));
    }
}
