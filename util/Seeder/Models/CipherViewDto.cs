using System.Text.Json.Serialization;

namespace Bit.Seeder.Models;

public class CipherViewDto
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid? OrganizationId { get; set; }

    [JsonPropertyName("folderId")]
    public Guid? FolderId { get; set; }

    [JsonPropertyName("collectionIds")]
    public List<Guid> CollectionIds { get; set; } = [];

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("login")]
    public LoginViewDto? Login { get; set; }

    [JsonPropertyName("identity")]
    public object? Identity { get; set; }

    [JsonPropertyName("card")]
    public object? Card { get; set; }

    [JsonPropertyName("secureNote")]
    public object? SecureNote { get; set; }

    [JsonPropertyName("sshKey")]
    public object? SshKey { get; set; }

    [JsonPropertyName("favorite")]
    public bool Favorite { get; set; }

    [JsonPropertyName("reprompt")]
    public int Reprompt { get; set; }

    [JsonPropertyName("organizationUseTotp")]
    public bool OrganizationUseTotp { get; set; }

    [JsonPropertyName("edit")]
    public bool Edit { get; set; } = true;

    [JsonPropertyName("permissions")]
    public object? Permissions { get; set; }

    [JsonPropertyName("viewPassword")]
    public bool ViewPassword { get; set; } = true;

    [JsonPropertyName("localData")]
    public object? LocalData { get; set; }

    [JsonPropertyName("attachments")]
    public object? Attachments { get; set; }

    [JsonPropertyName("fields")]
    public List<FieldViewDto>? Fields { get; set; }

    [JsonPropertyName("passwordHistory")]
    public object? PasswordHistory { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deletedDate")]
    public DateTime? DeletedDate { get; set; }

    [JsonPropertyName("revisionDate")]
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("archivedDate")]
    public DateTime? ArchivedDate { get; set; }
}

public class LoginViewDto
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("passwordRevisionDate")]
    public DateTime? PasswordRevisionDate { get; set; }

    [JsonPropertyName("uris")]
    public List<LoginUriViewDto>? Uris { get; set; }

    [JsonPropertyName("totp")]
    public string? Totp { get; set; }

    [JsonPropertyName("autofillOnPageLoad")]
    public bool? AutofillOnPageLoad { get; set; }

    [JsonPropertyName("fido2Credentials")]
    public object? Fido2Credentials { get; set; }
}

public class LoginUriViewDto
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("match")]
    public int? Match { get; set; }

    [JsonPropertyName("uriChecksum")]
    public string? UriChecksum { get; set; }
}

public class FieldViewDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("linkedId")]
    public int? LinkedId { get; set; }
}

public static class CipherTypes
{
    public const int Login = 1;
    public const int SecureNote = 2;
    public const int Card = 3;
    public const int Identity = 4;
    public const int SshKey = 5;
}

public static class RepromptTypes
{
    public const int None = 0;
    public const int Password = 1;
}
