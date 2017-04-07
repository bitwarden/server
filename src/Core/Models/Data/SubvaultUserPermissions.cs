using System;

namespace Bit.Core.Models.Data
{
    public class SubvaultUserPermissions
    {
        public Guid SubvaultId { get; set; }
        public bool ReadOnly { get; set; }
    }
}
