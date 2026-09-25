using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

/// <summary>
/// Account recovery keys an organization admin re-wrapped with members' V2 user keys, read from the members'
/// V2 upgrade tokens. An entry with no key unenrolls the member from account recovery instead.
/// </summary>
public class OrganizationUserV2UpgradesRequestModel : IValidatableObject
{
    [Required]
    [MinLength(1)]
    public required IReadOnlyList<OrganizationUserV2UpgradeRequestModel> Upgrades { get; init; }

    public IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> ToData() => Upgrades.Select(upgrade => upgrade.ToData());

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Two updates for one membership would make the outcome depend on write order.
        var hasDuplicates = Upgrades
            .GroupBy(upgrade => upgrade.OrganizationUserId)
            .Any(group => group.Count() > 1);

        if (hasDuplicates)
        {
            yield return new ValidationResult(
                "An organization user can only be upgraded once per request.",
                [nameof(Upgrades)]);
        }
    }
}

public class OrganizationUserV2UpgradeRequestModel : IValidatableObject
{
    [Required]
    public required Guid OrganizationUserId { get; init; }

    /// <summary>
    /// The key id of the user key the new <see cref="AccountRecoveryKey"/> wraps. The server checks it against the
    /// member's user row and rejects the whole request when it no longer matches. It is required to unenroll a
    /// member as well, so that a stale request cannot undo a rotation the admin has not read.
    /// </summary>
    [Required]
    [KeyId]
    public required string UserKeyId { get; init; }

    /// <summary>
    /// The member's V2 user key wrapped with the organization's public key, or no key to unenroll the member from
    /// account recovery. Send no key when the upgrade cannot be completed, for example when the V2 upgrade token
    /// does not contain a usable user key. The member keeps their vault, and the organization's enrollment policy
    /// prompts them to enroll again.
    /// </summary>
    public string? AccountRecoveryKey { get; init; }

    public OrganizationUserAccountRecoveryKeyUpdate ToData()
    {
        return new OrganizationUserAccountRecoveryKeyUpdate
        {
            OrganizationUserId = OrganizationUserId,
            UserKeyId = UserKeyId,
            // Store null, not a blank string, to match how the key is read
            AccountRecoveryKey = OrganizationUser.IsValidResetPasswordKey(AccountRecoveryKey)
                ? AccountRecoveryKey
                : null
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!OrganizationUser.IsValidResetPasswordKey(AccountRecoveryKey))
        {
            yield break;
        }

        // Validated here rather than with an attribute, because a blank key is meaningful. It unenrolls.
        var encryptedString = new EncryptedStringAttribute();
        if (!encryptedString.IsValid(AccountRecoveryKey))
        {
            yield return new ValidationResult(
                encryptedString.FormatErrorMessage(nameof(AccountRecoveryKey)),
                [nameof(AccountRecoveryKey)]);
        }
    }
}
