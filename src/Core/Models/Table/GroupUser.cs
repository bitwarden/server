using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Table
{
    public class GroupUser
    {
        public Guid GroupId { get; set; }
        public Guid OrganizationUserId { get; set; }
    }
}
