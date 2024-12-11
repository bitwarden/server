using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class EmergencyAccess : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid GrantorId { get; set; }
    public Guid? GranteeId { get; set; }

    [MaxLength(256)]
    public string Email { get; set; }
    public string KeyEncrypted { get; set; }
    public EmergencyAccessType Type { get; set; }
    public EmergencyAccessStatusType Status { get; set; }
    public int WaitTimeDays { get; set; }
    public DateTime? RecoveryInitiatedDate { get; set; }
    public DateTime? LastNotificationDate { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public EmergencyAccess ToEmergencyAccess()
    {
        return new EmergencyAccess
        {
            Id = Id,
            GrantorId = GrantorId,
            GranteeId = GranteeId,
            Email = Email,
            KeyEncrypted = KeyEncrypted,
            Type = Type,
            Status = Status,
            WaitTimeDays = WaitTimeDays,
            RecoveryInitiatedDate = RecoveryInitiatedDate,
            LastNotificationDate = LastNotificationDate,
            CreationDate = CreationDate,
            RevisionDate = RevisionDate,
        };
    }
}
