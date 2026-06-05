using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.Utilities;

/// <summary>
/// Source-generated JSON context for <see cref="ResetPasswordDataModel"/> with case-insensitive
/// property name matching. A separate context is used here because <see cref="ResetPasswordDataModel"/>
/// data can arrive from two different write paths:
/// <list type="bullet">
///   <item>
///     <description>
///       <c>Policy.SetDataModel&lt;T&gt;()</c> — serializes with <see cref="AdminConsoleJsonContext"/>
///       and always produces camelCase keys (e.g. <c>{"autoEnrollEnabled":true}</c>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>PolicyDataValidator.ValidateAndSerialize</c> — serializes a raw
///       <c>Dictionary&lt;string, object&gt;</c> with no naming policy, so the stored JSON preserves
///       whatever casing the API caller supplied (e.g. <c>{"AutoEnrollEnabled":true}</c>).
///     </description>
///   </item>
/// </list>
/// Isolating <c>PropertyNameCaseInsensitive = true</c> to this context avoids applying that overhead
/// to <see cref="Permissions"/> and other types that are always written with consistent camelCase,
/// where the case-insensitive lookup would impose a measurable deserialization regression.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ResetPasswordDataModel))]
internal partial class ResetPasswordJsonContext : JsonSerializerContext { }
