using System.Collections.Generic;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Business.Provider
{
    public class ProviderUserInvite
    {
        public IEnumerable<string> Emails { get; set; }
        public ProviderUserType? Type { get; set; }
        public Permissions Permissions { get; set; }

        public ProviderUserInvite() {}
    }
}
