using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api.Public
{
    public abstract class PolicyBaseModel
    {
        /// <summary>
        /// Determines if this policy is enabled and enforced.
        /// </summary>
        [Required]
        public bool? Enabled { get; set; }
        /// <summary>
        /// Data for the policy.
        /// </summary>
        [StringLength(300)]
        public Dictionary<string, object> Data { get; set; }
    }
}
