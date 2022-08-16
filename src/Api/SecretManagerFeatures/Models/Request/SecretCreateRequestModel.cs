using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request
{
    public class SecretCreateRequestModel : IValidatableObject
    {
        [Required]
        public Guid OrganizationId { get; set; }

        [Required]
        [EncryptedString]
        public string Key { get; set; }

        [Required]
        [EncryptedString]
        public string Value { get; set; }

        [EncryptedString]
        public string Note { get; set; }

        public Secret ToSecret()
        {
            return new Secret()
            {
                OrganizationId = this.OrganizationId,
                Key = this.Key,
                Value = this.Value,
                Note = this.Note,
                DeletedDate = null,
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

