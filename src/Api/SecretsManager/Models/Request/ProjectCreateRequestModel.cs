using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class ProjectCreateRequestModel
{
    [Required]
    [EncryptedString]
    public string Name { get; set; }

    public Project ToProject(Guid organizationId)
    {
        return new Project()
        {
            OrganizationId = organizationId,
            Name = Name,
        };
    }

    public AccessCheck ToAccessCheck(Guid organizationId)
    {
        return new AccessCheck() { OperationType = OperationType.CreateProject, OrganizationId = organizationId, };
    }
}
