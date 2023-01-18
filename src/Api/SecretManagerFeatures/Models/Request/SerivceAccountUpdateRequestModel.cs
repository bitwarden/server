using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretManagerFeatures.Models.Request;

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
}
