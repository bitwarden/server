using Bit.Core.Models.Table;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class ImportOrganizationUsersRequestModel
    {
        public Group[] Groups { get; set; }
        public User[] Users { get; set; }

        public class Group
        {
            [Required]
            public string Name { get; set; }
            [Required]
            public string ExternalId { get; set; }
            public IEnumerable<string> Users { get; set; }

            public Tuple<Table.Group, HashSet<string>> ToGroupTuple(Guid organizationId)
            {
                var group = new Table.Group
                {
                    OrganizationId = organizationId,
                    Name = Name,
                    ExternalId = ExternalId
                };

                return new Tuple<Table.Group, HashSet<string>>(group, new HashSet<string>(Users));
            }
        }

        public class User
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
            public bool Disabled { get; set; }
        }
    }
}
