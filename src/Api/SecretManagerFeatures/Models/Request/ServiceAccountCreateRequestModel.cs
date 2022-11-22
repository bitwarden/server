using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request;

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
}
