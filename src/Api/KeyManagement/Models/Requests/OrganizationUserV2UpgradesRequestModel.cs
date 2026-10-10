using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

/// <summary>
/// Account recovery keys an organization admin re-wrapped with members' V2 user keys, read from the members'
/// V2 upgrade tokens. An entry with a null key unenrolls the member from account recovery instead.
/// </summary>
public class OrganizationUserV2UpgradesRequestModel : IValidatableObject
{
    /// <summary>
    /// The maximum number of upgrades in one request and in one page of pending upgrades. It keeps each request
    /// within the gateway timeout.
    /// </summary>
    public const int MaxUpgrades = 100;

    [Required]
    [MinLength(1)]
    [MaxLength(MaxUpgrades)]
    [ValidateSequence<RequiredAttribute>]
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

public class OrganizationUserV2UpgradeRequestModel
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
    /// The member's V2 user key wrapped with the organization's public key, or null to unenroll the member from
    /// account recovery. Send null when the upgrade cannot be completed, for example when the V2 upgrade token
    /// does not contain a usable user key. The member keeps their vault, and the organization's enrollment policy
    /// prompts them to enroll again. A blank key is rejected, because only a key or its absence is meaningful.
    /// </summary>
    [EncryptedString]
    public string? AccountRecoveryKey { get; init; }

    public OrganizationUserAccountRecoveryKeyUpdate ToData()
    {
        return new OrganizationUserAccountRecoveryKeyUpdate
        {
            OrganizationUserId = OrganizationUserId,
            UserKeyId = UserKeyId,
            AccountRecoveryKey = AccountRecoveryKey
        };
    }
}
