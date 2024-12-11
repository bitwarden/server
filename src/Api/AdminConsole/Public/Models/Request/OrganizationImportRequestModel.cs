using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class OrganizationImportRequestModel
{
    /// <summary>
    /// Groups to import.
    /// </summary>
    public OrganizationImportGroupRequestModel[] Groups { get; set; }

    /// <summary>
    /// Members to import.
    /// </summary>
    public OrganizationImportMemberRequestModel[] Members { get; set; }

    /// <summary>
    /// Determines if the data in this request should overwrite or append to the existing organization data.
    /// </summary>
    [Required]
    public bool? OverwriteExisting { get; set; }

    /// <summary>
    /// Indicates an import of over 2000 users and/or groups is expected
    /// </summary>
    public bool LargeImport { get; set; } = false;

    public class OrganizationImportGroupRequestModel
    {
        /// <summary>
        /// The name of the group.
        /// </summary>
        /// <example>Development Team</example>
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// External identifier for reference or linking this group to another system, such as a user directory.
        /// </summary>
        /// <example>external_id_123456</example>
        [Required]
        [StringLength(300)]
        [JsonConverter(typeof(PermissiveStringConverter))]
        public string ExternalId { get; set; }

        /// <summary>
        /// The associated external ids for members in this group.
        /// </summary>
        [JsonConverter(typeof(PermissiveStringEnumerableConverter))]
        public IEnumerable<string> MemberExternalIds { get; set; }

        public ImportedGroup ToImportedGroup(Guid organizationId)
        {
            var importedGroup = new ImportedGroup
            {
                Group = new Group
                {
                    OrganizationId = organizationId,
                    Name = Name,
                    ExternalId = ExternalId,
                },
                ExternalUserIds = new HashSet<string>(MemberExternalIds),
            };

            return importedGroup;
        }
    }

    public class OrganizationImportMemberRequestModel : IValidatableObject
    {
        /// <summary>
        /// The member's email address. Required for non-deleted users.
        /// </summary>
        /// <example>jsmith@example.com</example>
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; }

        /// <summary>
        /// External identifier for reference or linking this member to another system, such as a user directory.
        /// </summary>
        /// <example>external_id_123456</example>
        [Required]
        [StringLength(300)]
        [JsonConverter(typeof(PermissiveStringConverter))]
        public string ExternalId { get; set; }

        /// <summary>
        /// Determines if this member should be removed from the organization during import.
        /// </summary>
        public bool Deleted { get; set; }

        public ImportedOrganizationUser ToImportedOrganizationUser()
        {
            var importedUser = new ImportedOrganizationUser
            {
                Email = Email.ToLowerInvariant(),
                ExternalId = ExternalId,
            };

            return importedUser;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Email) && !Deleted)
            {
                yield return new ValidationResult(
                    "Email is required for enabled members.",
                    new string[] { nameof(Email) }
                );
            }
        }
    }
}
