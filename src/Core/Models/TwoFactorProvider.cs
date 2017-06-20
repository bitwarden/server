using System.Collections.Generic;

namespace Bit.Core.Models
{
    public class TwoFactorProvider
    {
        public bool Enabled { get; set; }
        public Dictionary<string, string> MetaData { get; set; } = new Dictionary<string, string>();
    }
}
