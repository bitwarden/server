using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class UnlockMethodRequestModel : IValidatableObject
{
    // Master password user
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlockData { get; init; }

    // Key Connector user.
    [EncryptedString]
    public string? KeyConnectorKeyWrappedUserKey { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KeyConnectorKeyWrappedUserKey != null && MasterPasswordUnlockData != null)
        {
            yield return new ValidationResult("Invalid request user can't have KeyConnectorKeyWrappedUserKey and MasterPasswordUnlockData");
        }
    }

    public UnlockMethod GetUnlockMethod()
    {
        if (KeyConnectorKeyWrappedUserKey == null && MasterPasswordUnlockData == null)
        {
            return UnlockMethod.Tde;
        }

        if (KeyConnectorKeyWrappedUserKey != null && MasterPasswordUnlockData == null)
        {
            return UnlockMethod.KeyConnector;
        }

        if (MasterPasswordUnlockData != null && KeyConnectorKeyWrappedUserKey == null)
        {
            return UnlockMethod.MasterPassword;
        }

        throw new BadRequestException("Unknown UnlockMethod");
    }
}
