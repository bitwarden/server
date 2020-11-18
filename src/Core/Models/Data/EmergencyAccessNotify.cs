using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data
{
    public class EmergencyAccessNotify: EmergencyAccess
    {
        public string GrantorEmail { get; set; }
        public string GranteeName { get; set; }
    }
}
