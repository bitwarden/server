using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountCreateRequestModel
{
    [Required]
    [EncryptedString]
    public string Name { get; set; }

    public ServiceAccount ToServiceAccount(Guid organizationId)
    {
        return new ServiceAccount()
        {
            OrganizationId = organizationId,
            Name = Name,
        };
    }

    public AccessCheck ToAccessCheck(Guid organizationId, Guid userId)
    {
        return new AccessCheck
        {
            OperationType = OperationType.CreateServiceAccount,
            OrganizationId = organizationId,
            UserId = userId,
        };
    }
}
