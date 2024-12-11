using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class Installation : ITableObject<Guid>
{
    public Guid Id { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = null!;

    [MaxLength(150)]
    public string Key { get; set; } = null!;
    public bool Enabled { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
