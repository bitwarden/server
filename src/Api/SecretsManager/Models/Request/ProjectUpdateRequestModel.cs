using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class ProjectUpdateRequestModel
{
    [Required]
    [EncryptedString]
    public string Name { get; set; }

    public Project ToProject(Guid id)
    {
        return new Project
        {
            Id = id,
            Name = Name,
        };
    }

    public AccessCheck ToAccessCheck(Guid organizationId, Guid id, Guid userId)
    {
        return new AccessCheck
        {
            AccessOperationType = AccessOperationType.UpdateProject,
            OrganizationId = organizationId,
            TargetId = id,
            UserId = userId,
        };
    }
}
