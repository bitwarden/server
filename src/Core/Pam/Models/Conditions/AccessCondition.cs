using System.Text.Json.Serialization;

namespace Bit.Core.Pam.Models.Conditions;

/// <summary>
/// Base type for the structured conditions document stored on <c>AccessRule.Conditions</c>.
/// Polymorphic deserialization is keyed by the JSON <c>kind</c> property.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HumanApprovalCondition), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistCondition), "ip_allowlist")]
[JsonDerivedType(typeof(TimeOfDayCondition), "time_of_day")]
[JsonDerivedType(typeof(AllOfCondition), "all_of")]
public abstract class AccessCondition;
