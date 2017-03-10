using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserInviteRequestModel
    {
        public string Email { get; set; }
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
        public IEnumerable<Subvault> Subvaults { get; set; }

        public class Subvault
        {
            public string Id { get; set; }
            public string SubvaultId { get; set; }
            public bool Admin { get; set; }
            public bool ReadOnly { get; set; }

            public SubvaultUser ToSubvaultUser()
            {
                var user = new SubvaultUser
                {
                    SubvaultId = new Guid(SubvaultId),
                    Admin = Admin,
                    ReadOnly = ReadOnly
                };

                if(string.IsNullOrWhiteSpace(Id))
                {
                    user.Id = new Guid(Id);
                }

                return user;
            }
        }
    }
}
