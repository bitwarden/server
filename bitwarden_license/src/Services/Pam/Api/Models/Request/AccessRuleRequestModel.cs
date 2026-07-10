using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Api.Models.Request;

public class AccessRuleRequestModel : IValidatableObject
{
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
    /// The conditions that govern when this access rule permits a request.
    /// Each condition is a typed union discriminated by the <c>kind</c> field.
    /// Supported kinds: <c>human_approval</c> (bare object, no payload) and
    /// <c>ip_allowlist</c> (requires a non-empty <c>cidrs</c> list of canonical,
    /// host-bit-free CIDR strings). Unknown kinds are rejected (fail-closed).
    /// The field itself is required: omitting it fails validation, while an explicitly empty
    /// array is valid and means the rule imposes no conditions.
    /// Maximum 10 conditions per rule.
    /// </summary>
    [Required]
    [MaxLength(10)]
    public List<AccessConditionModel> Conditions { get; set; } = null!;

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
        // Recurse into condition items — Validator.TryValidateObject does not walk collection
        // elements automatically, so we validate each condition and prefix member names with the
        // index so callers can identify which item failed.
        for (var i = 0; i < Conditions.Count; i++)
        {
            var condition = Conditions[i];
            // JSON binding produces a null element for a literal null in the array; reject it here
            // rather than letting ValidationContext throw.
            if (condition is null)
            {
                yield return new ValidationResult(
                    $"Conditions[{i}] must not be null.",
                    [$"Conditions[{i}]"]);
                continue;
            }

            // A condition that bound to the base type had no 'kind' discriminator (binding falls
            // back to the concrete base rather than throwing — see AccessConditionModel). Reject
            // it instead of accepting a meaningless no-op condition.
            if (condition.GetType() == typeof(AccessConditionModel))
            {
                yield return new ValidationResult(
                    $"Conditions[{i}] must include a 'kind' discriminator.",
                    [$"Conditions[{i}]"]);
                continue;
            }

            var conditionContext = new ValidationContext(condition);
            var conditionResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(condition, conditionContext, conditionResults, validateAllProperties: true))
            {
                foreach (var result in conditionResults)
                {
                    var memberNames = result.MemberNames
                        .Select(m => $"Conditions[{i}].{m}")
                        .DefaultIfEmpty($"Conditions[{i}]")
                        .ToArray();
                    yield return new ValidationResult(result.ErrorMessage, memberNames);
                }
            }
        }
    }
}
