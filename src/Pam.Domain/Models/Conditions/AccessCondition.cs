using System.Text.Json.Serialization;

namespace Bit.Pam.Models.Conditions;

/// <summary>
/// Base type for a single leaf condition in an access rule's flat conditions list. Polymorphic deserialization is
/// keyed by the JSON <c>kind</c> property.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HumanApprovalCondition), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistCondition), "ip_allowlist")]
[JsonDerivedType(typeof(TimeOfDayCondition), "time_of_day")]
public abstract class AccessCondition;
