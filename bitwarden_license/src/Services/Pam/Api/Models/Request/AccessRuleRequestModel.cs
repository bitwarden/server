using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Bit.Services.Pam.Api.Models.Request;

public class AccessRuleRequestModel : IValidatableObject
{
    // Conditions are carried on the wire as verbatim JSON and stored verbatim, so the generated
    // SDK clients bind them as an opaque value rather than a typed union (openapi-generator turns a
    // oneOf into a lossy untagged enum). Server-side we still validate fail-closed by decoding that
    // JSON a second time into the typed AccessConditionModel union in Validate() — see below. These
    // options mirror the binding defaults (camelCase) and, like the SDK's serde tagging, accept the
    // `kind` discriminator at any position in the object.
    private static readonly JsonSerializerOptions ConditionsSerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
        };

    /// <summary>
    /// The maximum number of conditions a single rule may carry.
    /// </summary>
    private const int MaxConditions = 10;

    /// <summary>
    /// The rule's display name, shown wherever rules are listed and managed. Required; up to 256 characters.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Optional free-text describing the rule's intent. Has no effect on evaluation; surfaced to admins only.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When false, the rule is inactive and does not gate access for the collections it governs. Defaults to
    /// true so a request that omits the field creates an active rule.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The conditions that govern when this access rule permits a request — for example requiring human
    /// approval, or restricting to certain source IPs. Carried and stored as verbatim JSON: an array of
    /// kind-tagged objects (<c>{"kind":"human_approval"}</c>, <c>{"kind":"ip_allowlist","cidrs":[...]}</c>).
    /// The array itself is required (an explicitly empty array means the rule imposes no conditions); its
    /// contents are validated fail-closed server-side by decoding each entry into a typed condition union.
    /// Supported kinds: <c>human_approval</c> (bare object, no payload) and <c>ip_allowlist</c> (a non-empty
    /// <c>cidrs</c> list of canonical, host-bit-free CIDR strings). Unknown kinds, unknown members, and
    /// malformed payloads are rejected. Maximum 10 conditions.
    /// </summary>
    [Required]
    public JsonElement? Conditions { get; set; }

    /// <summary>
    /// When true, the rule enforces a per-cipher singleton (at most one active lease per cipher across all users).
    /// </summary>
    public bool SingleActiveLease { get; set; }

    /// <summary>
    /// Default lease duration in seconds, used to pre-fill a request opened under this rule. Null means the
    /// backend default applies.
    /// </summary>
    public int? DefaultLeaseDurationSeconds { get; set; }

    /// <summary>
    /// Hard ceiling on the duration of any single lease granted under this rule, in seconds. Null means no
    /// per-rule cap.
    /// </summary>
    public int? MaxLeaseDurationSeconds { get; set; }

    /// <summary>
    /// When true, a member holding an active lease under this rule may extend it once (always auto-approved), by up
    /// to <see cref="MaxExtensionDurationSeconds"/>.
    /// </summary>
    public bool AllowsExtensions { get; set; }

    /// <summary>
    /// The longest a single extension may run, in seconds. Required to be positive when
    /// <see cref="AllowsExtensions"/> is true.
    /// </summary>
    public int? MaxExtensionDurationSeconds { get; set; }

    /// <summary>
    /// The complete set of collections this rule governs. The rule's associations are replaced to match
    /// exactly this set; an empty array clears all associations.
    /// </summary>
    [Required]
    public IEnumerable<Guid> Collections { get; set; } = null!;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // A null/absent conditions field is handled by [Required]; nothing more to do here.
        if (Conditions is not { } conditions || conditions.ValueKind == JsonValueKind.Null)
        {
            yield break;
        }

        if (conditions.ValueKind != JsonValueKind.Array)
        {
            yield return new ValidationResult(
                "Conditions must be an array of condition objects.",
                [nameof(Conditions)]);
            yield break;
        }

        if (conditions.GetArrayLength() > MaxConditions)
        {
            yield return new ValidationResult(
                $"A rule may have at most {MaxConditions} conditions.",
                [nameof(Conditions)]);
        }

        var index = 0;
        foreach (var element in conditions.EnumerateArray())
        {
            var member = $"{nameof(Conditions)}[{index}]";
            index++;

            AccessConditionModel? condition = null;
            var decodeFailed = false;
            try
            {
                // The second decode: verbatim JSON -> typed union. Unknown kinds, unknown members,
                // non-object elements, and non-string discriminators all fail closed here rather
                // than being silently accepted (which would enforce a condition more loosely than
                // the caller intended).
                condition = element.Deserialize<AccessConditionModel>(ConditionsSerializerOptions);
            }
            catch (JsonException)
            {
                decodeFailed = true;
            }

            if (decodeFailed)
            {
                // The offending input is identified by index only — user input must not appear in
                // error messages or logs.
                yield return new ValidationResult($"{member} is not a valid condition.", [member]);
                continue;
            }

            // A JSON null element decodes to null.
            if (condition is null)
            {
                yield return new ValidationResult($"{member} must not be null.", [member]);
                continue;
            }

            // A payload with no `kind` binds to the concrete base type (see AccessConditionModel);
            // reject it rather than accepting a meaningless no-op condition.
            if (condition.GetType() == typeof(AccessConditionModel))
            {
                yield return new ValidationResult(
                    $"{member} must include a 'kind' discriminator.",
                    [member]);
                continue;
            }

            // Recurse into the decoded condition — Validator does not walk nested objects — and
            // prefix member names with the index so callers can identify which item failed.
            var conditionContext = new ValidationContext(condition);
            var conditionResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(condition, conditionContext, conditionResults, validateAllProperties: true))
            {
                foreach (var result in conditionResults)
                {
                    var memberNames = result.MemberNames
                        .Select(m => $"{member}.{m}")
                        .DefaultIfEmpty(member)
                        .ToArray();
                    yield return new ValidationResult(result.ErrorMessage, memberNames);
                }
            }
        }
    }
}
