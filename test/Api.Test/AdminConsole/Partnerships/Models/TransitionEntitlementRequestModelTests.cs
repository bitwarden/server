using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Api.AdminConsole.Partnerships.Models.Request;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Partnerships.Models;

public class TransitionEntitlementRequestModelTests
{
    private static readonly JsonSerializerOptions _apiOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("billing_lapse", PartnershipEntitlementReason.BillingLapse)]
    [InlineData("customer_request", PartnershipEntitlementReason.CustomerRequest)]
    [InlineData("subscription_ended", PartnershipEntitlementReason.SubscriptionEnded)]
    [InlineData("fraud_hold", PartnershipEntitlementReason.FraudHold)]
    [InlineData("plan_change", PartnershipEntitlementReason.PlanChange)]
    [InlineData("administrative", PartnershipEntitlementReason.Administrative)]
    public void Deserialize_PartnerReason_MapsToEnum(string reason, PartnershipEntitlementReason expected)
    {
        var model = Deserialize($$"""{"reason":"{{reason}}"}""");

        Assert.Equal(expected, model.Reason);
    }

    [Theory]
    [InlineData("\"lapsed\"")]
    [InlineData("1")]
    public void Deserialize_UnknownReason_Throws(string reason)
    {
        Assert.Throws<JsonException>(() => Deserialize($$"""{"reason":{{reason}}}"""));
    }

    [Fact]
    public void Deserialize_EmptyObject_LeavesEveryFieldUnset()
    {
        var model = Deserialize("{}");

        Assert.Null(model.Reason);
        Assert.Null(model.EffectiveAt);
        Assert.Null(model.PartnerEventId);
    }

    [Fact]
    public void ToRequest_MapsFieldsAndConvertsEffectiveAtToUtc()
    {
        var partnershipId = Guid.NewGuid();
        var model = Deserialize(
            """{"reason":"fraud_hold","effectiveAt":"2026-10-16T08:00:00-04:00","partnerEventId":"evt_123"}""");

        var request = model.ToRequest(partnershipId, "customer-1", PartnershipEntitlementAction.Suspend);

        Assert.Equal(partnershipId, request.OrganizationPartnershipId);
        Assert.Equal("customer-1", request.ExternalId);
        Assert.Equal(PartnershipEntitlementAction.Suspend, request.Action);
        Assert.Equal(PartnershipEntitlementReason.FraudHold, request.Reason);
        Assert.Equal(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc), request.EffectiveAt);
        Assert.Equal(DateTimeKind.Utc, request.EffectiveAt!.Value.Kind);
        Assert.Equal("evt_123", request.PartnerEventId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("evt_123")]
    [InlineData("A-z.0_~:@9")]
    public void Validate_AllowedPartnerEventId_IsValid(string? partnerEventId)
    {
        Assert.True(IsValid(new TransitionEntitlementRequestModel { PartnerEventId = partnerEventId }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("evt 123")]
    [InlineData("evt/123")]
    [InlineData("évt")]
    [InlineData("evt\n")]
    public void Validate_DisallowedPartnerEventId_IsInvalid(string partnerEventId)
    {
        Assert.False(IsValid(new TransitionEntitlementRequestModel { PartnerEventId = partnerEventId }));
    }

    [Fact]
    public void Validate_PartnerEventIdAtMaxLength_IsValid()
    {
        var partnerEventId = new string('a', TransitionEntitlementRequestModel.PartnerEventIdMaxLength);

        Assert.True(IsValid(new TransitionEntitlementRequestModel { PartnerEventId = partnerEventId }));
    }

    [Fact]
    public void Validate_PartnerEventIdOverMaxLength_IsInvalid()
    {
        var partnerEventId = new string('a', TransitionEntitlementRequestModel.PartnerEventIdMaxLength + 1);

        Assert.False(IsValid(new TransitionEntitlementRequestModel { PartnerEventId = partnerEventId }));
    }

    private static TransitionEntitlementRequestModel Deserialize(string json) =>
        JsonSerializer.Deserialize<TransitionEntitlementRequestModel>(json, _apiOptions)!;

    private static bool IsValid(TransitionEntitlementRequestModel model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), [], validateAllProperties: true);
}
