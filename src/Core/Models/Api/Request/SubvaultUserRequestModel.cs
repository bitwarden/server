using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class SubvaultUserSubvaultRequestModel
    {
        public string UserId { get; set; }
        public IEnumerable<Subvault> Subvaults { get; set; }

        public IEnumerable<SubvaultUser> ToSubvaultUsers()
        {
            return Subvaults.Select(s => new SubvaultUser
            {
                OrganizationUserId = new Guid(UserId),
                SubvaultId = new Guid(s.SubvaultId),
                ReadOnly = s.ReadOnly
            });
        }

        public class Subvault
        {
            public string SubvaultId { get; set; }
            public bool ReadOnly { get; set; }
        }
    }

    public class SubvaultUserUserRequestModel
    {
        public string UserId { get; set; }
        public bool ReadOnly { get; set; }
    }
}
