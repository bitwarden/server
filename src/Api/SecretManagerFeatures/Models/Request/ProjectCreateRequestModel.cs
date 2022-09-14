using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request
{
    public class ProjectCreateRequestModel
    {
        [Required]
        public Guid OrganizationId { get; set; }

        [Required]
        [EncryptedString]
        public string Name { get; set; }

        public Project ToProject(Guid organizationId)
        {
            return new Project()
            {
                OrganizationId = organizationId,
                Name = this.Name,
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (OrganizationId == default(Guid))
            {
                yield return new ValidationResult("Organization ID is required.");
            }
        }
    }
}
