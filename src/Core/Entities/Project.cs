#nullable enable
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class Project : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string? Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }

    public DateTime? DeletedDate { get; set; }

    public ICollection<Secret>? Secrets { get; set; }

    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
