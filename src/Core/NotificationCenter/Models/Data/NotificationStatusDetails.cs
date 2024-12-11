#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Enums;

namespace Bit.Core.NotificationCenter.Models.Data;

public class NotificationStatusDetails
{
    // Notification fields
    public Guid Id { get; set; }
    public Priority Priority { get; set; }
    public bool Global { get; set; }
    public ClientType ClientType { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }

    [MaxLength(256)]
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }

    // Notification Status fields
    public DateTime? ReadDate { get; set; }
    public DateTime? DeletedDate { get; set; }
}
