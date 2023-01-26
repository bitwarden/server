using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class SecretCreateRequestModel
{
    [Required]
    [EncryptedString]
    public string Key { get; set; }

    [Required]
    [EncryptedString]
    public string Value { get; set; }

    [Required]
    [EncryptedString]
    public string Note { get; set; }

    public Guid[] ProjectIds { get; set; }

    public Secret ToSecret(Guid organizationId)
    {
        return new Secret()
        {
            OrganizationId = organizationId,
            Key = Key,
            Value = Value,
            Note = Note,
            DeletedDate = null,
            Projects = ProjectIds != null && ProjectIds.Any() ? ProjectIds.Select(x => new Project() { Id = x }).ToList() : null,
        };
    }
}
