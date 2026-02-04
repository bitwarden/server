using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;
using System.ComponentModel.DataAnnotations;

public enum RegisterFinishTokenType : byte
{
    EmailVerification = 1,
    OrganizationInvite = 2,
    OrgSponsoredFreeFamilyPlan = 3,
    EmergencyAccessInvite = 4,
    ProviderInvite = 5,
}

public class RegisterFinishRequestModel : IValidatableObject
{
    [StrictEmailAddress, StringLength(256)]
    public required string Email { get; set; }
    public string? EmailVerificationToken { get; set; }

    public MasterPasswordAuthenticationDataRequestModel? MasterPasswordAuthentication { get; set; }
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlock { get; set; }

    // PM-28143 - Remove property below (made optional during migration to MasterPasswordUnlockData)
    [StringLength(1000)]
    // Made optional but there will still be a thrown error if it does not exist either here or
    // in the MasterPasswordAuthenticationData.
    public string? MasterPasswordHash { get; set; }

    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    // PM-28143 - Remove property below (made optional during migration to MasterPasswordUnlockData)
    // Made optional but there will still be a thrown error if it does not exist either here or
    // in the MasterPasswordAuthenticationData.
    public string? UserSymmetricKey { get; set; }

    // TODO Remove property below, deprecated due to new AccountKeys property
    // https://bitwarden.atlassian.net/browse/PM-27326
    // Will throw error if both UserAsymmetricKeys and AccountKeys do not exist.
    public KeysRequestModel? UserAsymmetricKeys { get; set; }

    public AccountKeysRequestModel? AccountKeys { get; set; }

    // PM-28143 - Remove line below (made optional during migration to MasterPasswordUnlockData)
    public KdfType? Kdf { get; set; }
    // PM-28143 - Remove line below (made optional during migration to MasterPasswordUnlockData)
    public int? KdfIterations { get; set; }
    // PM-28143 - Remove line below
    public int? KdfMemory { get; set; }
    // PM-28143 - Remove line below
    public int? KdfParallelism { get; set; }

    public Guid? OrganizationUserId { get; set; }
    public string? OrgInviteToken { get; set; }

    public string? OrgSponsoredFreeFamilyPlanToken { get; set; }

    public string? AcceptEmergencyAccessInviteToken { get; set; }
    public Guid? AcceptEmergencyAccessId { get; set; }

    public string? ProviderInviteToken { get; set; }

    public Guid? ProviderUserId { get; set; }

    public User ToUser(bool IsV2Encryption)
    {
        // TODO remove IsV2Encryption bool and simplify logic below after a compatibility period - once V2 accounts are supported
        // https://bitwarden.atlassian.net/browse/PM-27326
        if (!IsV2Encryption)
        {
            var user = new User
            {
                Email = Email,
                MasterPasswordHint = MasterPasswordHint,
                Kdf = (KdfType)(MasterPasswordUnlock?.Kdf.KdfType ?? Kdf)!,
                KdfIterations = (int)(MasterPasswordUnlock?.Kdf.Iterations ?? KdfIterations)!,
                // KdfMemory and KdfParallelism are optional (only used for Argon2id)
                KdfMemory = MasterPasswordUnlock?.Kdf.Memory ?? KdfMemory,
                KdfParallelism = MasterPasswordUnlock?.Kdf.Parallelism ?? KdfParallelism,
                // PM-28827 To be added when MasterPasswordSalt is added to the user column
                // MasterPasswordSalt = MasterPasswordUnlock?.Salt ?? Email.ToLower().Trim(),
                Key = MasterPasswordUnlock?.MasterKeyWrappedUserKey ?? UserSymmetricKey
            };

            user = UserAsymmetricKeys?.ToUser(user) ?? throw new Exception("User's public and private account keys couldn't be found in either AccountKeys or UserAsymmetricKeys");

            return user;
        }
        return new User
        {
            Email = Email,
            MasterPasswordHint = MasterPasswordHint,
        };
    }

    public RegisterFinishData ToData()
    {
        // TODO clean up flow once old fields are deprecated
        // https://bitwarden.atlassian.net/browse/PM-27326
        return new RegisterFinishData
        {
            MasterPasswordUnlockData = MasterPasswordUnlock?.ToData() ??
                new MasterPasswordUnlockData
                {
                    Kdf = new KdfSettings
                    {
                        KdfType = Kdf ?? throw new Exception("KdfType couldn't be found on either the MasterPasswordUnlockData or the Kdf property passed in."),
                        Iterations = KdfIterations ?? throw new Exception("KdfIterations couldn't be found on either the MasterPasswordUnlockData or the KdfIterations property passed in."),
                        // KdfMemory and KdfParallelism are optional (only used for Argon2id)
                        Memory = KdfMemory,
                        Parallelism = KdfParallelism,
                    },
                    MasterKeyWrappedUserKey = UserSymmetricKey ?? throw new Exception("MasterKeyWrappedUserKey couldn't be found on either the MasterPasswordUnlockData or the UserSymmetricKey property passed in."),
                    // PM-28827 To be added when MasterPasswordSalt is added to the user column
                    Salt = Email.ToLowerInvariant().Trim(),
                },
            UserAccountKeysData = AccountKeys?.ToAccountKeysData() ??
                new UserAccountKeysData
                {
                    PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData
                    (
                        UserAsymmetricKeys?.EncryptedPrivateKey ??
                            throw new Exception("WrappedPrivateKey couldn't be found in either AccountKeys or UserAsymmetricKeys."),
                        UserAsymmetricKeys?.PublicKey ??
                            throw new Exception("PublicKey couldn't be found in either AccountKeys or UserAsymmetricKeys")
                    ),
                },
            MasterPasswordAuthenticationData = MasterPasswordAuthentication?.ToData() ??
                new MasterPasswordAuthenticationData
                {
                    Kdf = new KdfSettings
                    {
                        KdfType = Kdf ?? throw new Exception("KdfType couldn't be found on either the MasterPasswordUnlockData or the Kdf property passed in."),
                        Iterations = KdfIterations ?? throw new Exception("KdfIterations couldn't be found on either the MasterPasswordUnlockData or the KdfIterations property passed in."),
                        // KdfMemory and KdfParallelism are optional (only used for Argon2id)
                        Memory = KdfMemory,
                        Parallelism = KdfParallelism,
                    },
                    MasterPasswordAuthenticationHash = MasterPasswordHash ?? throw new BadRequestException("MasterPasswordHash couldn't be found on either the MasterPasswordAuthenticationData or the MasterPasswordHash property passed in."),
                    Salt = Email.ToLowerInvariant().Trim(),
                }
        };
    }

    public RegisterFinishTokenType GetTokenType()
    {
        if (!string.IsNullOrWhiteSpace(EmailVerificationToken))
        {
            return RegisterFinishTokenType.EmailVerification;
        }
        if (!string.IsNullOrEmpty(OrgInviteToken)
            && OrganizationUserId.HasValue
            && OrganizationUserId.Value != Guid.Empty)
        {
            return RegisterFinishTokenType.OrganizationInvite;
        }
        if (!string.IsNullOrWhiteSpace(OrgSponsoredFreeFamilyPlanToken))
        {
            return RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan;
        }
        if (!string.IsNullOrWhiteSpace(AcceptEmergencyAccessInviteToken)
            && AcceptEmergencyAccessId.HasValue
            && AcceptEmergencyAccessId.Value != Guid.Empty)
        {
            return RegisterFinishTokenType.EmergencyAccessInvite;
        }
        if (!string.IsNullOrWhiteSpace(ProviderInviteToken)
            && ProviderUserId.HasValue
            && ProviderUserId.Value != Guid.Empty)
        {
            return RegisterFinishTokenType.ProviderInvite;
        }

        throw new InvalidOperationException("Invalid token type.");
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 1. Authentication data containing hash and hash at root level check
        if (MasterPasswordAuthentication != null && MasterPasswordHash != null)
        {
            if (MasterPasswordAuthentication.MasterPasswordAuthenticationHash != MasterPasswordHash)
            {
                yield return new ValidationResult(
                    $"{nameof(MasterPasswordAuthentication.MasterPasswordAuthenticationHash)} and root level {nameof(MasterPasswordHash)} provided and are not equal. Only provide one.",
                    [nameof(MasterPasswordAuthentication.MasterPasswordAuthenticationHash), nameof(MasterPasswordHash)]);
            }
        } // 1.5 if there is no master password hash that is unacceptable even though they are both optional in the model
        else if (MasterPasswordAuthentication == null && MasterPasswordHash == null)
        {
            yield return new ValidationResult(
                $"{nameof(MasterPasswordAuthentication.MasterPasswordAuthenticationHash)} and {nameof(MasterPasswordHash)} not found on request, one needs to be defined.",
                [nameof(MasterPasswordAuthentication.MasterPasswordAuthenticationHash), nameof(MasterPasswordHash)]);
        }

        // 2. Validate kdf settings.
        if (MasterPasswordUnlock != null)
        {
            foreach (var validationResult in KdfSettingsValidator.Validate(MasterPasswordUnlock.ToData().Kdf))
            {
                yield return validationResult;
            }
        }

        if (MasterPasswordAuthentication != null)
        {
            foreach (var validationResult in KdfSettingsValidator.Validate(MasterPasswordAuthentication.ToData().Kdf))
            {
                yield return validationResult;
            }
        }

        // 3. Validate root kdf values if kdf values are not in the unlock and authentication.
        if (MasterPasswordUnlock == null && MasterPasswordAuthentication == null)
        {
            var hasMissingRequiredKdfInputs = false;
            if (Kdf == null)
            {
                yield return new ValidationResult($"{nameof(Kdf)} not found on RequestModel", [nameof(Kdf)]);
                hasMissingRequiredKdfInputs = true;
            }
            if (KdfIterations == null)
            {
                yield return new ValidationResult($"{nameof(KdfIterations)} not found on RequestModel", [nameof(KdfIterations)]);
                hasMissingRequiredKdfInputs = true;
            }

            if (!hasMissingRequiredKdfInputs)
            {
                foreach (var validationResult in KdfSettingsValidator.Validate(
                             Kdf!.Value,
                             KdfIterations!.Value,
                             KdfMemory,
                             KdfParallelism))
                {
                    yield return validationResult;
                }
            }
        }
        else if (MasterPasswordUnlock == null && MasterPasswordAuthentication != null)
        {
            // Authentication provided but Unlock missing
            yield return new ValidationResult($"{nameof(MasterPasswordUnlock)} not found on RequestModel", [nameof(MasterPasswordUnlock)]);
        }
        else if (MasterPasswordUnlock != null && MasterPasswordAuthentication == null)
        {
            // Unlock provided but Authentication missing
            yield return new ValidationResult($"{nameof(MasterPasswordAuthentication)} not found on RequestModel", [nameof(MasterPasswordAuthentication)]);
        }

        if (AccountKeys == null && UserAsymmetricKeys == null)
        {
            yield return new ValidationResult(
                $"{nameof(AccountKeys.PublicKeyEncryptionKeyPair.PublicKey)} and {nameof(AccountKeys.PublicKeyEncryptionKeyPair.WrappedPrivateKey)} not found in RequestModel",
                [nameof(AccountKeys.PublicKeyEncryptionKeyPair.PublicKey), nameof(AccountKeys.PublicKeyEncryptionKeyPair.WrappedPrivateKey)]);
        }

        // 3. Lastly, validate access token type and presence. Must be done last because of yield break.
        RegisterFinishTokenType tokenType;
        var tokenTypeResolved = true;
        try
        {
            tokenType = GetTokenType();
        }
        catch (InvalidOperationException)
        {
            tokenTypeResolved = false;
            tokenType = default;
        }

        if (!tokenTypeResolved)
        {
            yield return new ValidationResult("No valid registration token provided");
            yield break;
        }

        switch (tokenType)
        {
            case RegisterFinishTokenType.EmailVerification:
                if (string.IsNullOrEmpty(EmailVerificationToken))
                {
                    yield return new ValidationResult(
                        $"{nameof(EmailVerificationToken)} absent when processing register/finish.",
                        [nameof(EmailVerificationToken)]);
                }
                break;
            case RegisterFinishTokenType.OrganizationInvite:
                if (string.IsNullOrEmpty(OrgInviteToken))
                {
                    yield return new ValidationResult(
                        $"{nameof(OrgInviteToken)} absent when processing register/finish.",
                        [nameof(OrgInviteToken)]);
                }
                break;
            case RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan:
                if (string.IsNullOrEmpty(OrgSponsoredFreeFamilyPlanToken))
                {
                    yield return new ValidationResult(
                        $"{nameof(OrgSponsoredFreeFamilyPlanToken)} absent when processing register/finish.",
                        [nameof(OrgSponsoredFreeFamilyPlanToken)]);
                }
                break;
            case RegisterFinishTokenType.EmergencyAccessInvite:
                if (string.IsNullOrEmpty(AcceptEmergencyAccessInviteToken))
                {
                    yield return new ValidationResult(
                        $"{nameof(AcceptEmergencyAccessInviteToken)} absent when processing register/finish.",
                        [nameof(AcceptEmergencyAccessInviteToken)]);
                }
                if (!AcceptEmergencyAccessId.HasValue || AcceptEmergencyAccessId.Value == Guid.Empty)
                {
                    yield return new ValidationResult(
                        $"{nameof(AcceptEmergencyAccessId)} absent when processing register/finish.",
                        [nameof(AcceptEmergencyAccessId)]);
                }
                break;
            case RegisterFinishTokenType.ProviderInvite:
                if (string.IsNullOrEmpty(ProviderInviteToken))
                {
                    yield return new ValidationResult(
                        $"{nameof(ProviderInviteToken)} absent when processing register/finish.",
                        [nameof(ProviderInviteToken)]);
                }
                if (!ProviderUserId.HasValue || ProviderUserId.Value == Guid.Empty)
                {
                    yield return new ValidationResult(
                        $"{nameof(ProviderUserId)} absent when processing register/finish.",
                        [nameof(ProviderUserId)]);
                }
                break;
            default:
                yield return new ValidationResult("Invalid registration finish request");
                break;
        }
    }
}
