using System.Text.Json.Serialization;
using Bit.Services.Pam.Engine;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Base type for a single leaf condition in an access rule's flat conditions list. Polymorphic deserialization is
/// keyed by the JSON <c>kind</c> property.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HumanApprovalCondition), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistCondition), "ip_allowlist")]
[JsonDerivedType(typeof(TimeOfDayCondition), "time_of_day")]
public abstract class AccessCondition
{
    /// <summary>
    /// Evaluates this condition against the request-time <paramref name="signals"/>, returning whether it allows,
    /// denies, or requires human approval. The engine folds each condition's result into the rule's overall
    /// decision; it does not know how any individual kind decides.
    /// </summary>
    public abstract AccessEvaluation Evaluate(AccessSignals signals);

    /// <summary>
    /// Checks this condition is well-formed at write time, before it is persisted, returning an actionable error
    /// when it is not. The loud, reject-on-save counterpart to <see cref="Evaluate"/>'s fail-closed runtime behavior.
    /// </summary>
    public abstract AccessRuleValidationResult Validate();
}
