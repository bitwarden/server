using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationInviteLink : ITableObject<Guid>
{
    public Guid Id { get; set; }
    /// <summary>
    /// A random, publicly shareable code used to identify the invite link.
    /// Uses <see cref="Guid.NewGuid"/> rather than a sequential/comb GUID because this is not
    /// a table identifier and therefore does not need index-friendly ordering. A comb GUID's embedded
    /// timestamp would also make the code partially predictable.
    /// </summary>
    public Guid Code { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string AllowedDomains { get; set; } = null!;
    public string EncryptedInviteKey { get; set; } = null!;
    public string? EncryptedOrgKey { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public IEnumerable<string> GetAllowedDomains() =>
        JsonSerializer.Deserialize<IEnumerable<string>>(AllowedDomains) ?? [];

    public void SetAllowedDomains(IEnumerable<string> domains) =>
        AllowedDomains = JsonSerializer.Serialize(domains);

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
