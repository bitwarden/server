using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.SecretsManager.Entities;

public class ApiKey : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid? ServiceAccountId { get; set; }
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    [MaxLength(128)]
    public string? ClientSecretHash { get; set; }
    [MaxLength(4000)]
    public string Scope { get; set; } = null!;
    [MaxLength(4000)]
    public string EncryptedPayload { get; set; } = null!;
    // Key for decrypting `EncryptedPayload`. Encrypted using the organization key.
    public string Key { get; set; } = null!;
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
