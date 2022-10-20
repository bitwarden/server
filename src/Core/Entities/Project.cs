#nullable enable
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class Project : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string? Name { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedDate { get; set; }

    public virtual ICollection<Secret>? Secrets { get; set; }

    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
