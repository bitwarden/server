using Bit.Core.Models.Business;
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

            public ImportedGroup ToImportedGroup(Guid organizationId)
            {
                var importedGroup = new ImportedGroup
                {
                    Group = new Table.Group
                    {
                        OrganizationId = organizationId,
                        Name = Name,
                        ExternalId = ExternalId
                    },
                    ExternalUserIds = new HashSet<string>(Users)
                };

                return importedGroup;
            }
        }

        public class User : IValidatableObject
        {
            [EmailAddress]
            public string Email { get; set; }
            public bool Disabled { get; set; }
            [Required]
            public string ExternalId { get; set; }

            public ImportedOrganizationUser ToImportedOrganizationUser()
            {
                var importedUser = new ImportedOrganizationUser
                {
                    Email = Email,
                    ExternalId = ExternalId
                };

                return importedUser;
            }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if(string.IsNullOrWhiteSpace(Email) && !Disabled)
                {
                    yield return new ValidationResult("Email is required for enabled users.", new string[] { nameof(Email) });
                }
            }
        }
    }
}
