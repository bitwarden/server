using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api.Public
{
    public class PolicyCreateRequestModel : PolicyUpdateRequestModel
    {
        /// <summary>
        /// The type of policy.
        /// </summary>
        [Required]
        public Enums.PolicyType? Type { get; set; }

        public Policy ToPolicy(Guid orgId)
        {
            return ToPolicy(new Policy
            {
                OrganizationId = orgId
            });
        }
    }
}
