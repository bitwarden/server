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
    /// Double-dispatches to the <paramref name="visitor"/>'s method for this condition's concrete kind, used by the
    /// validator to check each kind is well-formed. The compiler requires the visitor to handle every kind, so a
    /// newly added condition can't be silently skipped by validation.
    /// </summary>
    public abstract T Accept<T>(IAccessConditionVisitor<T> visitor);
}
