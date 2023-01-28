using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api;

public class PushSendRequestModel : IValidatableObject
{
    public string UserId { get; set; }
    public string OrganizationId { get; set; }
    public string DeviceId { get; set; }
    public string Identifier { get; set; }
    [Required]
    public PushType? Type { get; set; }
    [Required]
    public object Payload { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(OrganizationId))
        {
            yield return new ValidationResult($"{nameof(UserId)} or {nameof(OrganizationId)} is required.");
        }
    }
}
