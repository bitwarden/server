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
    [EncryptedStringLength(35000)]
    public string Value { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(10000)]
    public string Note { get; set; }

    public Guid[] ProjectIds { get; set; }

    public SecretAccessPoliciesRequestsModel AccessPoliciesRequests { get; set; }

    public Secret ToSecret(Secret secret)
    {
        secret.Key = Key;
        secret.Value = Value;
        secret.Note = Note;
        secret.RevisionDate = DateTime.UtcNow;

        if (secret.Projects?.FirstOrDefault()?.Id == ProjectIds?.FirstOrDefault())
        {
            secret.Projects = null;
        }
        else
        {
            secret.Projects =
                ProjectIds != null && ProjectIds.Length != 0
                    ? ProjectIds.Select(x => new Project() { Id = x }).ToList()
                    : [];
        }

        return secret;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProjectIds is { Length: > 1 })
        {
            yield return new ValidationResult(
                $"Only one project assignment is supported.",
                new[] { nameof(ProjectIds) }
            );
        }
    }
}
