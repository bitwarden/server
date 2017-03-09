using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core
{
    public class CurrentContext
    {
        public virtual User User { get; set; }
        public virtual string DeviceIdentifier { get; set; }
    }
}
