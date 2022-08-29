using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class Send : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public SendType Type { get; set; }
    public string Data { get; set; }
    public string Key { get; set; }
    [MaxLength(300)]
    public string Password { get; set; }
    public int? MaxAccessCount { get; set; }
    public int AccessCount { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }
    public DateTime DeletionDate { get; set; }
    public bool Disabled { get; set; }
    public bool? HideEmail { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
