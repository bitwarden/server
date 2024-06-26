using System.Data;
using Bit.Core.Auth.Entities;

namespace Bit.Infrastructure.Dapper.Auth.Helpers;

public static class EmergencyAccessHelpers
{
    public static DataTable ToDataTable(this IEnumerable<EmergencyAccess> emergencyAccesses)
    {
        var emergencyAccessTable = new DataTable();

        var columnData = new List<(string name, Type type, Func<EmergencyAccess, object> getter)>
        {
            (nameof(EmergencyAccess.Id), typeof(Guid), c => c.Id),
            (nameof(EmergencyAccess.GrantorId), typeof(Guid), c => c.GrantorId),
            (nameof(EmergencyAccess.GranteeId), typeof(Guid), c => c.GranteeId),
            (nameof(EmergencyAccess.Email), typeof(string), c => c.Email),
            (nameof(EmergencyAccess.KeyEncrypted), typeof(string), c => c.KeyEncrypted),
            (nameof(EmergencyAccess.WaitTimeDays), typeof(int), c => c.WaitTimeDays),
            (nameof(EmergencyAccess.Type), typeof(short), c => c.Type),
            (nameof(EmergencyAccess.Status), typeof(short), c => c.Status),
            (nameof(EmergencyAccess.RecoveryInitiatedDate), typeof(DateTime), c => c.RecoveryInitiatedDate),
            (nameof(EmergencyAccess.LastNotificationDate), typeof(DateTime), c => c.LastNotificationDate),
            (nameof(EmergencyAccess.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(EmergencyAccess.RevisionDate), typeof(DateTime), c => c.RevisionDate),
        };

        return emergencyAccesses.BuildTable(emergencyAccessTable, columnData);
    }
}
