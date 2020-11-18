using System;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class EmergencyAccess : ITableObject<Guid>
    {
        public Guid Id { get; set; }
        public Guid GrantorId { get; set; }
        public Guid? GranteeId { get; set; }
        public string Email { get; set; }
        public string KeyEncrypted { get; set; }
        public EmergencyAccessType Type { get; set; }
        public EmergencyAccessStatusType Status { get; set; }
        public int WaitTimeDays { get; set; }
        public DateTime? RecoveryInitiatedAt { get; internal set; }
        public DateTime? LastNotificationAt { get; internal set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

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
                RecoveryInitiatedAt = RecoveryInitiatedAt,
                LastNotificationAt = LastNotificationAt,
                CreationDate = CreationDate,
                RevisionDate = RevisionDate,
            };
        }
    }
}
