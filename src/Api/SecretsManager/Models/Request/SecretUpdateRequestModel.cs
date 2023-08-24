using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class SecretUpdateRequestModel : IValidatableObject
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Key { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(5000)]
    public string Value { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(10000)]
    public string Note { get; set; }

    public Guid[] ProjectIds { get; set; }

    public Secret ToSecret(Guid id, Guid organizationId)
    {
        return new Secret()
        {
            Id = id,
            OrganizationId = organizationId,
            Key = Key,
            Value = Value,
            Note = Note,
            DeletedDate = null,
            Projects = ProjectIds != null && ProjectIds.Any() ? ProjectIds.Select(x => new Project() { Id = x }).ToList() : null,
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProjectIds is { Length: > 1 })
        {
            yield return new ValidationResult(
                $"Only one project assignment is supported.",
                new[] { nameof(ProjectIds) });
        }
    }
}
