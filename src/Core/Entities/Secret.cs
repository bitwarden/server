#nullable enable
using Bit.Core.Utilities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bit.Core.Entities;

public class Secret : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string? Key { get; set; }

    public string? Value { get; set; }

    public string? Note { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedDate { get; set; }

    [NotMapped]
    public IEnumerable<Guid>? ProjectGuids { get; set; }

    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
