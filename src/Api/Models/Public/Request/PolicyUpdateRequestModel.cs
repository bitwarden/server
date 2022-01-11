using System;
using Bit.Core.Entities;
using Bit.Core.Utilities;

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
            existingPolicy.Data = Data != null ? JsonHelpers.Serialize(Data) : null;
            return existingPolicy;
        }
    }
}
