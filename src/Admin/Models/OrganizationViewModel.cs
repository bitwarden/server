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

        public OrganizationViewModel(Organization org, IEnumerable<OrganizationUserUserDetails> orgUsers)
        {
            Organization = org;
            UserCount = orgUsers.Count();
            Owners = string.Join(", ", orgUsers.Where(u => u.Type == OrganizationUserType.Owner).Select(u => u.Email));
            Admins = string.Join(", ", orgUsers.Where(u => u.Type == OrganizationUserType.Admin).Select(u => u.Email));
        }

        public Organization Organization { get; set; }
        public string Owners { get; set; }
        public string Admins { get; set; }
        public int UserCount { get; set; }
    }
}
