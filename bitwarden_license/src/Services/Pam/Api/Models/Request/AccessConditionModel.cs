using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// Base class for access conditions. Each condition is discriminated by the <c>kind</c> field,
/// which may appear at any position in the object — the server enables
/// <see cref="System.Text.Json.JsonSerializerOptions.AllowOutOfOrderMetadataProperties"/> (see
/// <c>AddPamServices</c>) to match the SDK's serde tagging, which accepts the tag anywhere.
/// Supported kinds: <c>human_approval</c> (bare object, no payload) and <c>ip_allowlist</c>
/// (requires a non-empty <c>cidrs</c> list of canonical, host-bit-free CIDR strings). An unknown
/// kind — and any unknown member within a known kind — is rejected at deserialization time with
/// <see cref="System.Text.Json.JsonException"/> (fail-closed, surfaced as a 400 by request
/// binding): silently dropping an unrecognized constraint would enforce the condition more
/// loosely than the caller intended.
///
/// <para>Deliberately concrete, not abstract: a payload with no <c>kind</c> binds to this base
/// type instead of throwing <see cref="NotSupportedException"/> — which would escape request
/// binding as a 500 — and is then rejected by <see cref="AccessRuleRequestModel.Validate"/> with
/// a structured 400. Do not instantiate it outside of that binding fallback.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(HumanApprovalConditionModel), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistConditionModel), "ip_allowlist")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AccessConditionModel { }

/// <summary>
/// A condition requiring human approval before access is granted.
/// No additional payload — the presence of this condition in the rule's list is sufficient.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class HumanApprovalConditionModel : AccessConditionModel { }

/// <summary>
/// A condition restricting access to requests originating from one of the listed CIDR ranges.
/// The <c>cidrs</c> list must contain between 1 and 100 entries; each entry must be a canonical,
/// host-bit-free CIDR in the form <c>address/prefix</c>.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class IpAllowlistConditionModel : AccessConditionModel, IValidatableObject
{
    /// <summary>
    /// Between 1 and 100 CIDR ranges (e.g. <c>10.0.0.0/8</c>, <c>2001:db8::/32</c>).
    /// Each entry must be a canonical, host-bit-free CIDR string.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "An IP allowlist condition must contain at least one CIDR range.")]
    [MaxLength(100, ErrorMessage = "An IP allowlist condition may contain at most 100 CIDR ranges.")]
    public List<string> Cidrs { get; set; } = [];

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        for (var i = 0; i < Cidrs.Count; i++)
        {
            var cidr = Cidrs[i];
            // The length cap blocks degenerate-but-parseable forms (arbitrarily zero-padded
            // prefixes like "10.0.0.0/000...08") from being persisted; every legitimate CIDR fits
            // in 50 characters. The failing entry is identified by index only — user input must
            // stay out of error messages and logs.
            if (cidr is { Length: > 256 } || !CidrValidator.IsValid(cidr))
            {
                yield return new ValidationResult(
                    $"Cidrs[{i}] is not a valid canonical CIDR range.",
                    [$"{nameof(Cidrs)}[{i}]"]);
            }
        }
    }
}
