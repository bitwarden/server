using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Core.Auth.Models.Api.Request;

namespace Bit.Api.KeyManagement.Models.Requests;

public class UnlockDataRequestModel
{
    // All methods to get to the userkey
    public required MasterPasswordUnlockDataModel MasterPasswordUnlockData { get; set; }
    public required IEnumerable<EmergencyAccessWithIdRequestModel> EmergencyAccessUnlockData { get; set; }
    public required IEnumerable<ResetPasswordWithOrgIdRequestModel> OrganizationAccountRecoveryUnlockData { get; set; }
    public required IEnumerable<WebAuthnLoginRotateKeyRequestModel> PasskeyUnlockData { get; set; }
    public required IEnumerable<OtherDeviceKeysUpdateRequestModel> DeviceKeyUnlockData { get; set; }
}
