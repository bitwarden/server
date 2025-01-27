using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;

namespace Bit.Api.KeyManagement.Models.Request.Accounts;

public class UnlockDataRequestModel
{
    // All methods to get to the userkey
    [Required]
    public MasterPasswordUnlockDataModel MasterPasswordUnlockData { get; set; }
    public IEnumerable<EmergencyAccessWithIdRequestModel> EmergencyAccessUnlockData { get; set; }
    public IEnumerable<ResetPasswordWithOrgIdRequestModel> OrganizationAccountRecoveryUnlockData { get; set; }
    public IEnumerable<WebAuthnLoginRotateKeyRequestModel> PasskeyUnlockData { get; set; }
}
