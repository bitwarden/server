using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models.Request;

namespace Bit.Api.KeyManagement.Models.Request.Accounts;

public class RotateUserAccountKeysModel
{
    // Authentication for this requests
    [Required]
    public string OldMasterKeyServerAuthenticationHash { get; set; }

    // All methods to get to the userkey
    [Required]
    public MasterPasswordUnlockDataModel MasterPasswordUnlockData { get; set; }
    public IEnumerable<EmergencyAccessWithIdRequestModel> EmergencyAccessUnlockData { get; set; }
    public IEnumerable<ResetPasswordWithOrgIdRequestModel> OrganizationAccountRecoveryUnlockData { get; set; }
    public IEnumerable<WebAuthnLoginRotateKeyRequestModel> PasskeyPrfUnlockData { get; set; }

    // Other keys encrypted by the userkey
    [Required]
    public string UserKeyEncryptedAccountPrivateKey { get; set; }
    [Required]
    public string AccountPublicKey { get; set; }

    // User vault data encrypted by the userkey
    public IEnumerable<CipherWithIdRequestModel> Ciphers { get; set; }
    public IEnumerable<FolderWithIdRequestModel> Folders { get; set; }
    public IEnumerable<SendWithIdRequestModel> Sends { get; set; }

}
