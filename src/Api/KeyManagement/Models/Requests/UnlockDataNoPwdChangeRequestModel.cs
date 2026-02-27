using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;


namespace Bit.Api.KeyManagement.Models.Requests;

public class UnlockDataNoPwdChangeRequestModel : IValidatableObject
{
    // All methods to get to the userkey
    public required IEnumerable<EmergencyAccessWithIdRequestModel> EmergencyAccessUnlockData { get; set; }
    public required IEnumerable<ResetPasswordWithOrgIdRequestModel> OrganizationAccountRecoveryUnlockData { get; set; }
    public required IEnumerable<WebAuthnLoginRotateKeyRequestModel> PasskeyUnlockData { get; set; }
    public required IEnumerable<OtherDeviceKeysUpdateRequestModel> DeviceKeyUnlockData { get; set; }
    public V2UpgradeTokenRequestModel? V2UpgradeToken { get; set; }

    // Optional data depending on what type of user is doing key rotation (TDE, key connector, or master password user).
    // Master password user
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlockData { get; set; }

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

    public RequestType GetRequestType()
    {
        if (KeyConnectorKeyWrappedUserKey == null && MasterPasswordUnlockData == null)
        {
            return RequestType.Tde;
        }
        else if (KeyConnectorKeyWrappedUserKey != null && MasterPasswordUnlockData == null)
        {
            return RequestType.KeyConnector;
        }
        else if (MasterPasswordUnlockData != null && KeyConnectorKeyWrappedUserKey == null)
        {
            return RequestType.MasterPassword;
        }
        else
        {
            throw new NotImplementedException("Unknown RequestType");
        }
    }


    public enum RequestType
    {
        Tde,
        MasterPassword,
        KeyConnector,
    }

}
