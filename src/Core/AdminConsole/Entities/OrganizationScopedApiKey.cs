using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// An organization API key whose access tokens carry only the scopes stored on the key.
/// </summary>
/// <remarks>
/// Only a hash of the client secret is stored; the secret itself is never persisted.
/// </remarks>
public class OrganizationScopedApiKey : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    [MaxLength(200)]
    public required string Name { get; set; }
    /// <summary>
    /// Base64-encoded SHA-256 hash of the client secret.
    /// </summary>
    [MaxLength(128)]
    public required string ClientSecretHash { get; set; }
    /// <summary>
    /// A JSON-serialized list of scopes. Use <see cref="GetScopes"/> to read it.
    /// </summary>
    [MaxLength(4000)]
    public required string Scopes { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }

    public ICollection<string> GetScopes()
    {
        return CoreHelpers.LoadClassFromJsonData<List<string>>(Scopes);
    }
}
