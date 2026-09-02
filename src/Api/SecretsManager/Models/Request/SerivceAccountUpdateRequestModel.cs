// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.SecretsManager.Models.Request;

public class ServiceAccountUpdateRequestModel
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
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
