using Bit.Core.Enums;
using System.Collections.Generic;

namespace Bit.Core.Models
{
    public class TwoFactorProvider
    {
        public bool Enabled { get; set; }
        public bool Remember { get; set; }
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();
    }
}
