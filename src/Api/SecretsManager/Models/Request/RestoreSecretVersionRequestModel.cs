using System.ComponentModel.DataAnnotations;

namespace Bit.Api.SecretsManager.Models.Request;

public class RestoreSecretVersionRequestModel
{
    [Required]
    public Guid VersionId { get; set; }
}
