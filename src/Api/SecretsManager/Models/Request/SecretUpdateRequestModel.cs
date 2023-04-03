using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class SecretUpdateRequestModel
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

    public Secret ToSecret(Guid id)
    {
        return new Secret()
        {
            Id = id,
            Key = Key,
            Value = Value,
            Note = Note,
            DeletedDate = null,
            Projects = ProjectIds != null && ProjectIds.Any() ? ProjectIds.Select(x => new Project() { Id = x }).ToList() : null,
        };
    }

    public SecretAccessCheck ToSecretAccessCheck(Guid organizationId, Guid userId, Guid? currentSecretId)
    {
        var projectId = ProjectIds?.FirstOrDefault();
        return new SecretAccessCheck()
        {
            OperationType = OperationType.UpdateSecret,
            OrganizationId = organizationId,
            UserId = userId,
            TargetProjectId = projectId,
            CurrentSecretId = currentSecretId,
        };
    }
}
