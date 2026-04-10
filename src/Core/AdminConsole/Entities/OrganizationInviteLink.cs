using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationInviteLink : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid Code { get; set; }
    public Guid OrganizationId { get; set; }
    public string AllowedDomains { get; set; } = null!;
    public string EncryptedInviteKey { get; set; } = null!;
    public string? EncryptedOrgKey { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
