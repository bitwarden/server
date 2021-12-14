using System;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Api.Models.Public.Request
{
    public class PolicyUpdateRequestModel : PolicyBaseModel
    {
        public Policy ToPolicy(Guid orgId)
        {
            return ToPolicy(new Policy
            {
                OrganizationId = orgId
            });
        }

        public virtual Policy ToPolicy(Policy existingPolicy)
        {
            existingPolicy.Enabled = Enabled.GetValueOrDefault();
            existingPolicy.Data = Data != null ? JsonConvert.SerializeObject(Data) : null;
            return existingPolicy;
        }
    }
}
