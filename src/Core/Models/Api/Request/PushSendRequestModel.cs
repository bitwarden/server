#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api;

public class PushSendRequestModel<T> : IValidatableObject
{
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? Identifier { get; set; }
    public required PushType Type { get; set; }
    public required T Payload { get; set; }
    public ClientType? ClientType { get; set; }
    public Guid? InstallationId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!UserId.HasValue &&
            !OrganizationId.HasValue &&
            !InstallationId.HasValue)
        {
            yield return new ValidationResult(
                $"{nameof(UserId)} or {nameof(OrganizationId)} or {nameof(InstallationId)} is required.");
        }
    }
}
