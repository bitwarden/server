using Bit.Core.Enums;
using System.Collections.Generic;

namespace Bit.Core.Utilities
{
    public class EquivalentDomains
    {
        static EquivalentDomains()
        {
            Global = new Dictionary<GlobalEquivalentDomainsType, IEnumerable<string>>();

            Global.Add(GlobalEquivalentDomainsType.Apple, new List<string>() { "apple.com", "icloud.com" });
            Global.Add(GlobalEquivalentDomainsType.Google, new List<string> { "google.com", "youtube.com", "gmail.com" });
        }

        public static IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> Global { get; set; }

    }
}
