using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class PolicyRequestModel
    {
        [Required]
        public Enums.PolicyType? Type { get; set; }
        [Required]
        public bool? Enabled { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public Policy ToPolicy(Guid orgId)
        {
            return ToPolicy(new Policy
            {
                Type = Type.Value,
                OrganizationId = orgId
            });
        }

        public Policy ToPolicy(Policy existingPolicy)
        {
            existingPolicy.Enabled = Enabled.GetValueOrDefault();
            existingPolicy.Data = Data != null ? JsonConvert.SerializeObject(Data) : null;
            return existingPolicy;
        }
    }
}
