#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api;

public class PushSendRequestModel : IValidatableObject
{
    public string? UserId { get; set; }
    public string? OrganizationId { get; set; }
    public string? DeviceId { get; set; }
    public string? Identifier { get; set; }
    public required PushType Type { get; set; }
    public required object Payload { get; set; }
    public ClientType? ClientType { get; set; }
    public string? InstallationId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(UserId) &&
            string.IsNullOrWhiteSpace(OrganizationId) &&
            string.IsNullOrWhiteSpace(InstallationId))
        {
            yield return new ValidationResult(
                $"{nameof(UserId)} or {nameof(OrganizationId)} or {nameof(InstallationId)} is required.");
        }
    }
}
