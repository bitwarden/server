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

        // Property order carries no meaning in JSON, so a document that writes "kind" after the properties it
        // discriminates is legitimate and a client is free to emit one (anything that canonicalises keys
        // alphabetically does: "cidrs" sorts before "kind"). Without this, the polymorphic reader demands the
        // discriminator first and throws NotSupportedException, which is not a JsonException and so escapes the
        // handling at both call sites. Buffering the object costs nothing at these sizes: the validator caps a
        // conditions document at ten entries.
        AllowOutOfOrderMetadataProperties = true,
    };
}
