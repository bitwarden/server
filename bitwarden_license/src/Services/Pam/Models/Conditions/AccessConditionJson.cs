using System.Text.Json;

namespace Bit.Services.Pam.Models.Conditions;

/// <summary>
/// Serializer options for an access rule's conditions JSON. The write-time validator and the read-time resolver must
/// both use <see cref="Options"/>, so they accept the same documents.
/// </summary>
public static class AccessConditionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,

        // Accepts "kind" after the properties it discriminates, as a client that sorts keys writes it ("cidrs" first).
        // The buffering this costs is negligible, since the validator caps a document at ten conditions.
        AllowOutOfOrderMetadataProperties = true,
    };
}
