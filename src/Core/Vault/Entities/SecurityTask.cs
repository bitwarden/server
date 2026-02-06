using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Vault.Entities;

public class SecurityTask : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? CipherId { get; set; }
    public Enums.SecurityTaskType Type { get; set; }
    public Enums.SecurityTaskStatus Status { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
