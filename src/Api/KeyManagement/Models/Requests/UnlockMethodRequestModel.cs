using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class UnlockMethodRequestModel : IValidatableObject
{
    [Required]
    public required UnlockMethod UnlockMethod { get; init; }

    // Master password user
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlockData { get; init; }

    // Key Connector user.
    [EncryptedString]
    public string? KeyConnectorKeyWrappedUserKey { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (UnlockMethod)
        {
            case UnlockMethod.MasterPassword:
                if (MasterPasswordUnlockData == null || KeyConnectorKeyWrappedUserKey != null)
                {
                    yield return new ValidationResult("Invalid MasterPassword unlock method request, MasterPasswordUnlockData must be provided and KeyConnectorKeyWrappedUserKey must be null");
                }
                break;
            case UnlockMethod.Tde:
                if (MasterPasswordUnlockData != null || KeyConnectorKeyWrappedUserKey != null)
                {
                    yield return new ValidationResult("Invalid Tde unlock method request, MasterPasswordUnlockData must be null and KeyConnectorKeyWrappedUserKey must be null");
                }
                break;
            case UnlockMethod.KeyConnector:
                if (KeyConnectorKeyWrappedUserKey == null || MasterPasswordUnlockData != null)
                {
                    yield return new ValidationResult("Invalid KeyConnector unlock method request, KeyConnectorKeyWrappedUserKey must be provided and MasterPasswordUnlockData must be null");
                }
                break;
            default:
                yield return new ValidationResult("Unrecognized unlock method");
                break;
        }
    }
}
