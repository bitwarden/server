using System;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class SubvaultUserResponseModel : ResponseModel
    {
        public SubvaultUserResponseModel(SubvaultUserUserDetails subvaultUser)
            : base("subvaultUser")
        {
            if(subvaultUser == null)
            {
                throw new ArgumentNullException(nameof(subvaultUser));
            }

            Id = subvaultUser.Id?.ToString();
            OrganizationUserId = subvaultUser.OrganizationUserId.ToString();
            SubvaultId = subvaultUser.SubvaultId?.ToString();
            AccessAllSubvaults = subvaultUser.AccessAllSubvaults;
            Name = subvaultUser.Name;
            Email = subvaultUser.Email;
            Type = subvaultUser.Type;
            Status = subvaultUser.Status;
            ReadOnly = subvaultUser.ReadOnly;
        }

        public string Id { get; set; }
        public string OrganizationUserId { get; set; }
        public string SubvaultId { get; set; }
        public bool AccessAllSubvaults { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public OrganizationUserType Type { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public bool ReadOnly { get; set; }
    }
}
