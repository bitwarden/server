#nullable enable
using Bit.Core.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.NotificationCenter.Entities;

public class Notification : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Priority Priority { get; set; }
    public bool Global { get; set; }
    public ClientType ClientType { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
