using System.Text.Json.Serialization;

namespace Bit.Core.PrivilegedAccessManagement.Models.Policies;

/// <summary>
/// Base type for the structured leasing policy stored on <c>Collection.LeasingPolicy</c>.
/// Polymorphic deserialization is keyed by the JSON <c>kind</c> property.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(HumanApprovalPolicy), "human_approval")]
[JsonDerivedType(typeof(IpAllowlistPolicy), "ip_allowlist")]
[JsonDerivedType(typeof(TimeOfDayPolicy), "time_of_day")]
[JsonDerivedType(typeof(AllOfPolicy), "all_of")]
public abstract class LeasingPolicy;
