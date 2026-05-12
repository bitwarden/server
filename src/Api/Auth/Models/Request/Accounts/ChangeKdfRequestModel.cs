using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Utilities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

/// <summary>
/// Dual-shape request: validation accepts either the legacy
/// (<see cref="NewMasterPasswordHash"/>, <see cref="Key"/>) or new
/// (<see cref="AuthenticationData"/>, <see cref="UnlockData"/>) payload so the wire contract
/// can stabilize ahead of caller wiring. <c>PostKdf</c> currently honors only the new shape;
/// legacy-shape dispatch arrives with <c>ChangeKdfCommand</c>'s dual-path refactor. All legacy
/// fields are removed in PM-33141.
/// </summary>
public class ChangeKdfRequestModel : IValidatableObject
{
    [Required]
    public required string MasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; set; }

    // Should be made required in PM-33141
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    // Should be made required in PM-33141
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = AuthenticationData is not null && UnlockData is not null;
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

        foreach (var validationResult in MasterPasswordPayloadVariantValidator.ValidatePresence(
                     hasNewPayloads, hasLegacyPayloads))
        {
            yield return validationResult;
        }

        if (hasNewPayloads)
        {
            foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                         AuthenticationData!.ToData(), UnlockData!.ToData()))
            {
                yield return validationResult;
            }
        }
    }
}
