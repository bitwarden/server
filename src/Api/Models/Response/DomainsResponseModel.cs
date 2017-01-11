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
        public DomainsResponseModel(User user, bool excluded = true)
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
            var domainsToInclude = excluded ? Core.Utilities.EquivalentDomains.Global :
                Core.Utilities.EquivalentDomains.Global.Where(d => !excludedGlobalEquivalentDomains.Contains(d.Key));
            foreach(var domain in domainsToInclude)
            {
                globalDomains.Add(new GlobalDomains(domain.Key, domain.Value, excludedGlobalEquivalentDomains, excluded));
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
                IEnumerable<GlobalEquivalentDomainsType> excludedDomains,
                bool excluded)
            {
                Type = globalDomain;
                Domains = domains;
                Excluded = excluded && (excludedDomains?.Contains(globalDomain) ?? false);
            }

            public GlobalEquivalentDomainsType Type { get; set; }
            public IEnumerable<string> Domains { get; set; }
            public bool Excluded { get; set; }
        }
    }
}
