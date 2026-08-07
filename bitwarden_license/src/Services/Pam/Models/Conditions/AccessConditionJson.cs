using System.Text.Json;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// The single source of truth for how an access rule's conditions JSON is (de)serialized: camelCase property
/// names, read case-insensitively. Everything that parses the stored <c>Conditions</c> document — the validator
/// at write time and the resolver at read time — must use <see cref="Options"/> so the two never drift.
/// The accepted <c>kind</c> vocabulary itself lives on <see cref="AccessCondition"/>'s <c>[JsonDerivedType]</c>
/// attributes.
/// </summary>
public static class AccessConditionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
