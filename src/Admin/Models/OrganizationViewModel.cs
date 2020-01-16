using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Admin.Models
{
    public class OrganizationViewModel
    {
        public OrganizationViewModel() { }

        public OrganizationViewModel(Organization org, IEnumerable<OrganizationUserUserDetails> orgUsers,
            bool selfHosted = false, string adminEmail = null)
        {
            Organization = org;
            UserCount = orgUsers.Count();
            Owners = string.Join(", ", 
                orgUsers
                .Where(u => u.Type == OrganizationUserType.Owner && u.Status == OrganizationUserStatusType.Confirmed)
                .Select(u => u.Email));
            Admins = string.Join(", ", 
                orgUsers
                .Where(u => u.Type == OrganizationUserType.Admin && u.Status == OrganizationUserStatusType.Confirmed)
                .Select(u => u.Email));
            CanInviteMyself = selfHosted && orgUsers.Where(u => u.Email.Equals(adminEmail)).Count() == 0;
        }

        public Organization Organization { get; set; }
        public string Owners { get; set; }
        public string Admins { get; set; }
        public int UserCount { get; set; }
        public bool CanInviteMyself { get; set; }
    }
}
