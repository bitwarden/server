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
            IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections, IEnumerable<Group> groups,
            IEnumerable<Policy> policies)
        {
            Organization = org;
            UserCount = orgUsers.Count();
            CipherCount = ciphers.Count();
            CollectionCount = collections.Count();
            GroupCount = groups.Count();
            PolicyCount = policies.Count();
            Owners = string.Join(", ", 
                orgUsers
                .Where(u => u.Type == OrganizationUserType.Owner && u.Status == OrganizationUserStatusType.Confirmed)
                .Select(u => u.Email));
            Admins = string.Join(", ", 
                orgUsers
                .Where(u => u.Type == OrganizationUserType.Admin && u.Status == OrganizationUserStatusType.Confirmed)
                .Select(u => u.Email));
        }

        public Organization Organization { get; set; }
        public string Owners { get; set; }
        public string Admins { get; set; }
        public int UserCount { get; set; }
        public int CipherCount { get; set; }
        public int CollectionCount { get; set; }
        public int GroupCount { get; set; }
        public int PolicyCount { get; set; }
    }
}
