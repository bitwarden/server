using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class UpdateDomainsRequestModel
    {
        public IEnumerable<IEnumerable<string>> EquivalentDomains { get; set; }
        public IEnumerable<GlobalEquivalentDomainsType> ExcludedGlobalEquivalentDomains { get; set; }

        public User ToUser(User existingUser)
        {
            existingUser.EquivalentDomains = EquivalentDomains != null ? JsonConvert.SerializeObject(EquivalentDomains) : null;
            existingUser.ExcludedGlobalEquivalentDomains = ExcludedGlobalEquivalentDomains != null ?
                JsonConvert.SerializeObject(ExcludedGlobalEquivalentDomains) : null;
            return existingUser;
        }
    }
}
