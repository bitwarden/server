using System;
using System.Collections.Generic;

namespace Bit.Web.Models.Api.Public
{
    public class UpdateGroupIdsRequestModel
    {
        /// <summary>
        /// The associated group ids that this object can access.
        /// </summary>
        public IEnumerable<Guid> GroupIds { get; set; }
    }
}
