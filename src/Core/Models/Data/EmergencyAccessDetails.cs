using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class EmergencyAccessDetails : EmergencyAccess
    {
        public string GranteeName { get; set; }
        public string GranteeEmail { get; set; }
        public string GrantorName { get; set; }
        public string GrantorEmail { get; set; }
    }
}
