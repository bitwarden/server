using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class ImportOrganizationUsersRequestModel
{
    public Group[] Groups { get; set; }
    public User[] Users { get; set; }
    public bool OverwriteExisting { get; set; }
    public bool LargeImport { get; set; }

    public class Group
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        [Required]
        [StringLength(300)]
        public string ExternalId { get; set; }
        public IEnumerable<string> Users { get; set; }

        public ImportedGroup ToImportedGroup(Guid organizationId)
        {
            var importedGroup = new ImportedGroup
            {
                Group = new Core.Entities.Group
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
        [StringLength(256)]
        public string Email { get; set; }
        public bool Deleted { get; set; }
        [Required]
        [StringLength(300)]
        public string ExternalId { get; set; }

        public ImportedOrganizationUser ToImportedOrganizationUser()
        {
            var importedUser = new ImportedOrganizationUser
            {
                Email = Email.ToLowerInvariant(),
                ExternalId = ExternalId
            };

            return importedUser;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Email) && !Deleted)
            {
                yield return new ValidationResult("Email is required for enabled users.", new string[] { nameof(Email) });
            }
        }
    }
}
