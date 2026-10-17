using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

namespace Bit.Api.AdminConsole.Partnerships.Models.Request;

/// <summary>
/// Suspends, resumes, or cancels one of the partner's entitlements. Every field is optional; send <c>{}</c> to use the
/// defaults.
/// </summary>
public class TransitionEntitlementRequestModel
{
    public const int PartnerEventIdMaxLength = 128;

    /// <summary>
    /// Why the partner is making the change: <c>billing_lapse</c>, <c>customer_request</c>, <c>subscription_ended</c>,
    /// <c>fraud_hold</c>, <c>plan_change</c>, or <c>administrative</c>.
    /// </summary>
    [JsonConverter(typeof(SnakeCaseReasonConverter))]
    public PartnershipEntitlementReason? Reason { get; set; }

    /// <summary>
    /// When the change took effect at the partner. Defaults to now and must not be in the future. A change older than
    /// the entitlement's <c>lastAppliedAt</c> is acknowledged but not applied.
    /// </summary>
    public DateTimeOffset? EffectiveAt { get; set; }

    /// <summary>
    /// The partner's identifier for the event that triggered the change: 1 to 128 ASCII letters, digits, and
    /// <c>-._~:@</c>. Validated but not yet stored or returned; lifecycle events sent to the partner will carry it in a
    /// later release.
    /// </summary>
    [StringLength(PartnerEventIdMaxLength, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9._~:@-]+$")]
    public string? PartnerEventId { get; set; }

    public TransitionPartnershipEntitlementRequest ToRequest(Guid organizationPartnershipId, string externalId,
        PartnershipEntitlementAction action) =>
        new()
        {
            OrganizationPartnershipId = organizationPartnershipId,
            ExternalId = externalId,
            Action = action,
            Reason = Reason,
            EffectiveAt = EffectiveAt?.UtcDateTime,
            PartnerEventId = PartnerEventId,
        };

    private sealed class SnakeCaseReasonConverter()
        : JsonStringEnumConverter<PartnershipEntitlementReason>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false);
}
