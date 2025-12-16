using Bit.Core.Entities;
using Bit.Core.Enums;
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

    public MasterPasswordAuthenticationData? MasterPasswordAuthenticationData { get; set; }
    public MasterPasswordUnlockData? MasterPasswordUnlockData { get; set; }

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

    public required KeysRequestModel UserAsymmetricKeys { get; set; }

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

    public User ToUser()
    {
        var user = new User
        {
            Email = Email,
            MasterPasswordHint = MasterPasswordHint,
            Kdf = MasterPasswordUnlockData?.Kdf.KdfType ?? Kdf ?? throw new Exception("KdfType couldn't be found on either the MasterPasswordUnlockData or the Kdf property passed in."),
            KdfIterations = MasterPasswordUnlockData?.Kdf.Iterations ?? KdfIterations ?? throw new Exception("KdfIterations couldn't be found on either the MasterPasswordUnlockData or the KdfIterations property passed in."),
            // KdfMemory and KdfParallelism are optional (only used for Argon2id)
            KdfMemory = MasterPasswordUnlockData?.Kdf.Memory ?? KdfMemory,
            KdfParallelism = MasterPasswordUnlockData?.Kdf.Parallelism ?? KdfParallelism,
            // PM-28827 To be added when MasterPasswordSalt is added to the user column
            // MasterPasswordSalt = MasterPasswordUnlockData?.Salt ?? Email.ToLower().Trim(),
            Key = MasterPasswordUnlockData?.MasterKeyWrappedUserKey ?? UserSymmetricKey ?? throw new Exception("MasterKeyWrappedUserKey couldn't be found on either the MasterPasswordUnlockData or the UserSymmetricKey property passed in."),
        };

        UserAsymmetricKeys.ToUser(user);

        return user;
    }

    public RegisterFinishTokenType GetTokenType()
    {
        if (!string.IsNullOrWhiteSpace(EmailVerificationToken))
        {
            return RegisterFinishTokenType.EmailVerification;
        }
        if (!string.IsNullOrEmpty(OrgInviteToken) && OrganizationUserId.HasValue)
        {
            return RegisterFinishTokenType.OrganizationInvite;
        }
        if (!string.IsNullOrWhiteSpace(OrgSponsoredFreeFamilyPlanToken))
        {
            return RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan;
        }
        if (!string.IsNullOrWhiteSpace(AcceptEmergencyAccessInviteToken) && AcceptEmergencyAccessId.HasValue)
        {
            return RegisterFinishTokenType.EmergencyAccessInvite;
        }
        if (!string.IsNullOrWhiteSpace(ProviderInviteToken) && ProviderUserId.HasValue)
        {
            return RegisterFinishTokenType.ProviderInvite;
        }

        throw new InvalidOperationException("Invalid token type.");
    }


    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // PM-28143 - Remove line below
        var kdf = MasterPasswordUnlockData?.Kdf.KdfType
                  ?? Kdf
                  ?? throw new Exception($"{nameof(Kdf)} not found on RequestModel");

        // PM-28143 - Remove line below
        var kdfIterations = MasterPasswordUnlockData?.Kdf.Iterations
                            ?? KdfIterations
                            ?? throw new Exception($"{nameof(KdfIterations)} not found on RequestModel");

        // PM-28143 - Remove line below
        var kdfMemory = MasterPasswordUnlockData?.Kdf.Memory
                        ?? KdfMemory;

        // PM-28143 - Remove line below
        var kdfParallelism = MasterPasswordUnlockData?.Kdf.Parallelism
                             ?? KdfParallelism;

        // PM-28143 - Remove line below in favor of using the unlock data.
        return KdfSettingsValidator.Validate(kdf, kdfIterations, kdfMemory, kdfParallelism);

        // PM-28143 - Uncomment
        // return KdfSettingsValidator.Validate(MasterPasswordUnlockData);
    }
}
