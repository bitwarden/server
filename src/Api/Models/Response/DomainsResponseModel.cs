using System;
using Bit.Core.Domains;
using System.Collections.Generic;
using Newtonsoft.Json;
using Bit.Core.Enums;
using System.Linq;

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

            var excludedGlobalEquivalentDomains = user.ExcludedGlobalEquivalentDomains != null ?
                JsonConvert.DeserializeObject<List<GlobalEquivalentDomainsType>>(user.ExcludedGlobalEquivalentDomains) : null;
            var globalDomains = new List<GlobalDomains>();
            foreach(var domain in Core.Utilities.EquivalentDomains.Global)
            {
                globalDomains.Add(new GlobalDomains(domain.Key, domain.Value, excludedGlobalEquivalentDomains));
            }
            GlobalEquivalentDomains = !globalDomains.Any() ? null : globalDomains;
        }

        public IEnumerable<IEnumerable<string>> EquivalentDomains { get; set; }
        public IEnumerable<GlobalDomains> GlobalEquivalentDomains { get; set; }


        public class GlobalDomains
        {
            public GlobalDomains(
                GlobalEquivalentDomainsType globalDomain,
                IEnumerable<string> domains,
                IEnumerable<GlobalEquivalentDomainsType> excludedDomains)
            {
                Type = globalDomain;
                Domains = domains;
                Excluded = excludedDomains?.Contains(globalDomain) ?? false;
            }

            public GlobalEquivalentDomainsType Type { get; set; }
            public IEnumerable<string> Domains { get; set; }
            public bool Excluded { get; set; }
        }
    }
}
