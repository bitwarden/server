using System.Text.Json.Serialization;
using Bit.Core.KeyManagement.KeyService;

namespace Bit.Api.KeyManagement.Models.Responses;

/// <summary>
/// A Key Service response: <c>{"data": ...}</c>.
/// </summary>
public record KeyServiceDocument<T>(T Data);

/// <summary>
/// A Key Service resource: <c>{"id": ..., "type": ..., "attributes": {...}}</c>.
/// </summary>
public record KeyServiceResource<T>(Guid Id, string Type, T Attributes);

/// <summary>
/// A key's owner: <c>{"type": "user", "id": ...}</c>.
/// </summary>
public record KeyOwnerResponseModel(string Type, Guid Id)
{
    public static KeyOwnerResponseModel From(KeyOwner owner) => new(owner.Type switch
    {
        KeyOwnerType.User => "user",
        KeyOwnerType.Organization => "organization",
        KeyOwnerType.Provider => "provider",
        _ => throw new ArgumentOutOfRangeException(nameof(owner)),
    }, owner.Id);
}

public record PublicKeyAttributes(
    KeyOwnerResponseModel Owner,
    string KeyPurpose,
    string KeyId,
    DateTime CreatedAt,
    string Data)
{
    public static KeyServiceResource<PublicKeyAttributes> Resource(PublicKeyView key) =>
        new(key.Id, KeyResourceTypes.PublicKey,
            new(KeyOwnerResponseModel.From(key.Owner), key.KeyPurpose, key.KeyId, key.CreatedAt, key.Data));
}

public record OwnKeyAttributes(
    KeyOwnerResponseModel Owner,
    string KeyPurpose,
    string KeyId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SealedByKeyId,
    DateTime CreatedAt,
    string Data)
{
    public static KeyServiceResource<OwnKeyAttributes> Resource(OwnKeyView key) =>
        new(key.Id, key.Type,
            new(KeyOwnerResponseModel.From(key.Owner), key.KeyPurpose, key.KeyId, key.SealedByKeyId, key.CreatedAt,
                key.Data));
}

public record UnlockMethodAttributes(
    Guid User,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? Device,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? Passkey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? Organization,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContainedKeyId,
    DateTime CreatedAt,
    string Data)
{
    public static KeyServiceResource<UnlockMethodAttributes> Resource(UnlockMethodView method) =>
        new(method.Id, KeyResourceTypes.UnlockMethod,
            new(method.User, method.Kind, method.Device, method.Passkey, method.Organization, method.ContainedKeyId,
                method.CreatedAt, method.Data));
}
