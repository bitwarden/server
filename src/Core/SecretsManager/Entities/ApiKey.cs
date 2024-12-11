#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.SecretsManager.Entities;

public class ApiKey : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid? ServiceAccountId { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(128)]
    public string? ClientSecretHash { get; set; }

    [MaxLength(4000)]
    public required string Scope { get; set; }

    [MaxLength(4000)]
    public required string EncryptedPayload { get; set; }

    // Key for decrypting `EncryptedPayload`. Encrypted using the organization key.
    public required string Key { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public ICollection<string> GetScopes()
    {
        return CoreHelpers.LoadClassFromJsonData<List<string>>(Scope);
    }
}
