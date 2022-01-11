using System.Collections.Generic;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request
{
    public class UpdateDomainsRequestModel
    {
        public IEnumerable<IEnumerable<string>> EquivalentDomains { get; set; }
        public IEnumerable<GlobalEquivalentDomainsType> ExcludedGlobalEquivalentDomains { get; set; }

        public User ToUser(User existingUser)
        {
            existingUser.EquivalentDomains = EquivalentDomains != null ? JsonHelpers.Serialize(EquivalentDomains) : null;
            existingUser.ExcludedGlobalEquivalentDomains = ExcludedGlobalEquivalentDomains != null ?
                JsonHelpers.Serialize(ExcludedGlobalEquivalentDomains) : null;
            return existingUser;
        }
    }
}
