using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class Device : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    public Enums.DeviceType Type { get; set; }
    [MaxLength(50)]
    public string Identifier { get; set; }
    [MaxLength(255)]
    public string PushToken { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
