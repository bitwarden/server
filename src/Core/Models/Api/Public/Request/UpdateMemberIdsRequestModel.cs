using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api.Public
{
    public class UpdateMemberIdsRequestModel
    {
        /// <summary>
        /// The associated member ids that have access to this object.
        /// </summary>
        public IEnumerable<Guid> MemberIds { get; set; }
    }
}
