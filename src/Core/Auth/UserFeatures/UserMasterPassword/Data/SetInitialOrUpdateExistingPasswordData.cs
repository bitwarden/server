using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;

public class SetInitialOrUpdateExistingPasswordData
{
    public required MasterPasswordUnlockData MasterPasswordUnlock { get; set; }
    public required MasterPasswordAuthenticationData MasterPasswordAuthentication { get; set; }

    /// <summary>
    /// When <c>true</c>, runs the new password hash through the registered
    /// <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline before hashing.
    /// Set to <c>false</c> only in flows where password policy validation has already been enforced
    /// (e.g. admin-initiated recovery). Defaults to <c>true</c>.
    /// </summary>
    public bool ValidatePassword { get; set; } = true;
    /// <summary>
    /// When <c>true</c>, rotates <see cref="Bit.Core.Entities.User.SecurityStamp"/>, which invalidates
    /// all active sessions and authentication tokens for the user. Set to <c>false</c> only when
    /// intentionally preserving existing sessions. Defaults to <c>true</c>.
    /// </summary>
    public bool RefreshStamp { get; set; } = true;

    public string? MasterPasswordHint { get; set; } = null;

    public SetInitialPasswordData ToSetInitialData() => new()
    {
        MasterPasswordUnlock = MasterPasswordUnlock,
        MasterPasswordAuthentication = MasterPasswordAuthentication,
        ValidatePassword = ValidatePassword,
        RefreshStamp = RefreshStamp,
        MasterPasswordHint = MasterPasswordHint
    };

    public UpdateExistingPasswordData ToUpdateExistingData() => new()
    {
        MasterPasswordUnlock = MasterPasswordUnlock,
        MasterPasswordAuthentication = MasterPasswordAuthentication,
        ValidatePassword = ValidatePassword,
        RefreshStamp = RefreshStamp,
        MasterPasswordHint = MasterPasswordHint
    };
}
