using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api.Public
{
    public class PolicyUpdateRequestModel : PolicyBaseModel
    {
        public virtual Policy ToPolicy(Policy existingPolicy)
        {
            existingPolicy.Enabled = Enabled.GetValueOrDefault();
            existingPolicy.Data = Data != null ? JsonConvert.SerializeObject(Data) : null;
            return existingPolicy;
        }
    }
}
