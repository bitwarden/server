using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountUpdateRequestModel
{
    [Required]
    [EncryptedString]
    public string Name { get; set; }

    public ServiceAccount ToServiceAccount(Guid id)
    {
        return new ServiceAccount()
        {
            Id = id,
            Name = Name,
        };
    }

    public AccessCheck ToAccessCheck(Guid id, Guid organizationId, Guid userId)
    {
        return new AccessCheck
        {
            OperationType = OperationType.UpdateServiceAccount,
            OrganizationId = organizationId,
            TargetId = id,
            UserId = userId,
        };
    }
}
