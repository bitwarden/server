using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class GetSecretsRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}
