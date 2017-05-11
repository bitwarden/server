using System;
using Bit.Core.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class GroupUserResponseModel : ResponseModel
    {
        public GroupUserResponseModel(GroupUserDetails groupUser)
            : base("groupUser")
        {
            if(groupUser == null)
            {
                throw new ArgumentNullException(nameof(groupUser));
            }

            OrganizationUserId = groupUser.OrganizationUserId.ToString();
            AccessAll = groupUser.AccessAll;
            Name = groupUser.Name;
            Email = groupUser.Email;
            Type = groupUser.Type;
            Status = groupUser.Status;
        }

        public string OrganizationUserId { get; set; }
        public bool AccessAll { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
    }
}
