using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class OrganizationUserInviteRequestModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAllSubvaults { get; set; }
        public IEnumerable<OrganizationUserSubvaultRequestModel> Subvaults { get; set; }
    }

    public class OrganizationUserAcceptRequestModel
    {
        [Required]
        public string Token { get; set; }
    }

    public class OrganizationUserConfirmRequestModel
    {
        [Required]
        public string Key { get; set; }
    }

    public class OrganizationUserUpdateRequestModel
    {
        [Required]
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessAllSubvaults { get; set; }
        public IEnumerable<OrganizationUserSubvaultRequestModel> Subvaults { get; set; }

        public OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
        {
            existingUser.Type = Type.Value;
            existingUser.AccessAllSubvaults = AccessAllSubvaults;
            return existingUser;
        }
    }

    public class OrganizationUserSubvaultRequestModel
    {
        [Required]
        public string SubvaultId { get; set; }
        public bool ReadOnly { get; set; }

        public SubvaultUser ToSubvaultUser()
        {
            var subvault = new SubvaultUser
            {
                ReadOnly = ReadOnly,
                SubvaultId = new Guid(SubvaultId)
            };

            return subvault;
        }
    }
}
