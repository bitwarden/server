using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

public class EmergencyAccessNotify : EmergencyAccess
{
    public string GrantorEmail { get; set; }
    public string GranteeName { get; set; }
    public string GranteeEmail { get; set; }
}
