using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserInviteRequestModel
    {
        public string Email { get; set; }
        public IEnumerable<OrganizationUserSubvaultRequestModel> Subvaults { get; set; }
    }

    public class OrganizationUserAcceptRequestModel
    {
        public string Token { get; set; }
    }

    public class OrganizationUserConfirmRequestModel
    {
        public string Key { get; set; }
    }

    public class OrganizationUserUpdateRequestModel
    {
        public Enums.OrganizationUserType Type { get; set; }
        public IEnumerable<OrganizationUserSubvaultRequestModel> Subvaults { get; set; }
    }

    public class OrganizationUserSubvaultRequestModel
    {
        public string SubvaultId { get; set; }
        public bool Admin { get; set; }
        public bool ReadOnly { get; set; }

        public SubvaultUser ToSubvaultUser()
        {
            var subvault = new SubvaultUser
            {
                Admin = Admin,
                ReadOnly = ReadOnly
            };

            if(!string.IsNullOrWhiteSpace(SubvaultId))
            {
                subvault.SubvaultId = new Guid(SubvaultId);
            }

            return subvault;
        }
    }
}
