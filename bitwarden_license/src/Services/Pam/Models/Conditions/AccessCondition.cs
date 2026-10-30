using System.Text.Json.Serialization;
using Bit.Services.Pam.Engine;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>Base type for a single condition in an access rule's flat conditions list.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HumanApprovalCondition), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistCondition), "ip_allowlist")]
public abstract class AccessCondition
{
    /// <summary>
    /// Evaluates this condition against the request-time <paramref name="signals"/>. The engine folds each
    /// condition's result into the rule's decision without knowing how any kind decides.
    /// </summary>
    public abstract AccessEvaluation Evaluate(AccessSignals signals);

    /// <summary>
    /// Checks at write time that this condition is well-formed, returning an actionable error when it is not.
    /// <see cref="Evaluate"/> fails closed on a malformed condition instead.
    /// </summary>
    public abstract AccessRuleValidationResult Validate();
}
