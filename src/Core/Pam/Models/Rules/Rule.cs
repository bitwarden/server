using System.Text.Json.Serialization;

namespace Bit.Core.Pam.Models.Rules;

/// <summary>
/// Base type for the structured rule stored on <c>AccessRule.Rule</c>.
/// Polymorphic deserialization is keyed by the JSON <c>kind</c> property.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HumanApprovalRule), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistRule), "ip_allowlist")]
[JsonDerivedType(typeof(TimeOfDayRule), "time_of_day")]
[JsonDerivedType(typeof(AllOfRule), "all_of")]
public abstract class Rule;
