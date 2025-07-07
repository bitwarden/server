#nullable enable

using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Entities;

public class OrganizationApplication : ITableObject<Guid>, IRevisable
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Applications { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public string ContentEncryptionKey { get; set; } = string.Empty;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
