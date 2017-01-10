using System;
using Bit.Core.Domains;
using System.Collections.Generic;
using Newtonsoft.Json;
using Bit.Core.Enums;

namespace Bit.Api.Models
{
    public class DomainsResponseModel : ResponseModel
    {
        public DomainsResponseModel(User user)
            : base("domains")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            EquivalentDomains = user.EquivalentDomains != null ?
                JsonConvert.DeserializeObject<List<List<string>>>(user.EquivalentDomains) : null;
            GlobalEquivalentDomains = Core.Utilities.EquivalentDomains.Global;
            ExcludedGlobalEquivalentDomains = user.ExcludedGlobalEquivalentDomains != null ?
                JsonConvert.DeserializeObject<List<GlobalEquivalentDomainsType>>(user.ExcludedGlobalEquivalentDomains) : null;
        }

        public IEnumerable<IEnumerable<string>> EquivalentDomains { get; set; }
        public IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> GlobalEquivalentDomains { get; set; }
        public IEnumerable<GlobalEquivalentDomainsType> ExcludedGlobalEquivalentDomains { get; set; }
    }
}
