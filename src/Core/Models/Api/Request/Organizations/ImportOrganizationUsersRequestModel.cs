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
            
            public Table.Group ToGroup(Guid organizationId)
            {
                return new Table.Group
                {
                    OrganizationId = organizationId,
                    Name = Name,
                    ExternalId = ExternalId
                };
            }
        }

        public class User
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
            public IEnumerable<string> ExternalGroupIds { get; set; }

            public KeyValuePair<string, IEnumerable<string>> ToKvp()
            {
                return new KeyValuePair<string, IEnumerable<string>>(Email, ExternalGroupIds);
            }
        }
    }
}
