using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Portal.Models
{
    public class PoliciesModel
    {
        public PoliciesModel(ICollection<Policy> policies)
        {
            if (policies == null)
            {
                return;
            }

            var policyDict = policies?.ToDictionary(p => p.Type);
            Policies = new List<PolicyModel>();

            foreach (var type in Enum.GetValues(typeof(PolicyType)).Cast<PolicyType>())
            {
                var enabled = policyDict?.ContainsKey(type) ?? false && policyDict[type].Enabled;
                Policies.Add(new PolicyModel(type, enabled));
            }
        }

        public List<PolicyModel> Policies { get; set; }
    }
}
