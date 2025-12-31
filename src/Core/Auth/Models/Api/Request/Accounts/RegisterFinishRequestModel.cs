using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;
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

    // Strongly-typed accessors for post-validation usage to satisfy nullability
    // Ignore serialization, these are just null safe accessors.
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public string EmailVerificationTokenRequired =>
        EmailVerificationToken
        ?? throw new BadRequestException("Email verification token absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public string OrgInviteTokenRequired =>
        OrgInviteToken
        ?? throw new BadRequestException("Organization invite token absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Guid OrganizationUserIdRequired =>
        OrganizationUserId
        ?? throw new BadRequestException("Organization user id absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public string OrgSponsoredFreeFamilyPlanTokenRequired =>
        OrgSponsoredFreeFamilyPlanToken
        ?? throw new BadRequestException("Organization sponsored free family plan token absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public string AcceptEmergencyAccessInviteTokenRequired =>
        AcceptEmergencyAccessInviteToken
        ?? throw new BadRequestException("Accept emergency access invite token absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Guid AcceptEmergencyAccessIdRequired =>
        AcceptEmergencyAccessId
        ?? throw new BadRequestException("Accept emergency access id absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public string ProviderInviteTokenRequired =>
        ProviderInviteToken
        ?? throw new BadRequestException("Provider invite token absent when processing register/finish.");
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public Guid ProviderUserIdRequired =>
        ProviderUserId
        ?? throw new BadRequestException("Provider user id absent when processing register/finish.");

    public User ToUser()
    {
        var user = new User
        {
            Email = Email,
            MasterPasswordHint = MasterPasswordHint,
            Kdf = MasterPasswordUnlock?.Kdf.KdfType ?? Kdf
                ?? throw new BadRequestException("KdfType couldn't be found on either the MasterPasswordUnlockData or the Kdf property passed in."),
            KdfIterations = MasterPasswordUnlock?.Kdf.Iterations ?? KdfIterations
                ?? throw new BadRequestException("KdfIterations couldn't be found on either the MasterPasswordUnlockData or the KdfIterations property passed in."),
            // KdfMemory and KdfParallelism are optional (only used for Argon2id)
            KdfMemory = MasterPasswordUnlock?.Kdf.Memory ?? KdfMemory,
            KdfParallelism = MasterPasswordUnlock?.Kdf.Parallelism ?? KdfParallelism,
            // PM-28827 To be added when MasterPasswordSalt is added to the user column
            // MasterPasswordSalt = MasterPasswordUnlock?.Salt ?? Email.ToLower().Trim(),
            Key = MasterPasswordUnlock?.MasterKeyWrappedUserKey ?? UserSymmetricKey ?? throw new BadRequestException("MasterKeyWrappedUserKey couldn't be found on either the MasterPasswordUnlockData or the UserSymmetricKey property passed in."),
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
        // PM-28143 - Remove this check
        ThrowIfExistsAndHashIsNotEqual(MasterPasswordAuthentication, MasterPasswordHash);

        // 1. Access Token Presence Verification Check

        // Ensure exactly one registration token type is provided
        var hasEmailVerification = !string.IsNullOrWhiteSpace(EmailVerificationToken);
        var hasOrgInvite = !string.IsNullOrEmpty(OrgInviteToken) && OrganizationUserId.HasValue;
        var hasOrgSponsoredFreeFamilyPlan = !string.IsNullOrWhiteSpace(OrgSponsoredFreeFamilyPlanToken);
        var hasEmergencyAccessInvite = !string.IsNullOrWhiteSpace(AcceptEmergencyAccessInviteToken) && AcceptEmergencyAccessId.HasValue;
        var hasProviderInvite = !string.IsNullOrWhiteSpace(ProviderInviteToken) && ProviderUserId.HasValue;
        var tokenCount = (hasEmailVerification ? 1 : 0)
                         + (hasOrgInvite ? 1 : 0)
                         + (hasOrgSponsoredFreeFamilyPlan ? 1 : 0)
                         + (hasEmergencyAccessInvite ? 1 : 0)
                         + (hasProviderInvite ? 1 : 0);
        if (tokenCount == 0)
        {
            throw new BadRequestException("Invalid registration finish request");
        }
        if (tokenCount > 1)
        {
            throw new BadRequestException("Multiple registration token types provided.");
        }

        switch (GetTokenType())
        {
            case RegisterFinishTokenType.EmailVerification:
                if (string.IsNullOrEmpty(EmailVerificationToken))
                {
                    throw new BadRequestException("Email verification token absent when processing register/finish.");
                }
                break;
            case RegisterFinishTokenType.OrganizationInvite:
                if (string.IsNullOrEmpty(OrgInviteToken))
                {
                    throw new BadRequestException("Organization invite token absent when processing register/finish.");
                }
                break;
            case RegisterFinishTokenType.OrgSponsoredFreeFamilyPlan:
                if (string.IsNullOrEmpty(OrgSponsoredFreeFamilyPlanToken))
                {
                    throw new BadRequestException("Organization sponsored free family plan token absent when processing register/finish.");
                }
                break;
            case RegisterFinishTokenType.EmergencyAccessInvite:
                if (string.IsNullOrEmpty(AcceptEmergencyAccessInviteToken))
                {
                    throw new BadRequestException("Accept emergency access invite token absent when processing register/finish.");
                }
                if (!AcceptEmergencyAccessId.HasValue || AcceptEmergencyAccessId.Value == Guid.Empty)
                {
                    throw new BadRequestException("Accept emergency access id absent when processing register/finish.");
                }
                break;
            case RegisterFinishTokenType.ProviderInvite:
                if (string.IsNullOrEmpty(ProviderInviteToken))
                {
                    throw new BadRequestException("Provider invite token absent when processing register/finish.");
                }
                if (!ProviderUserId.HasValue || ProviderUserId.Value == Guid.Empty)
                {
                    throw new BadRequestException("Provider user id absent when processing register/finish.");
                }
                break;
            default:
                throw new BadRequestException("Invalid registration finish request");
        }

        // 2. Validate kdf settings.

        IEnumerable<ValidationResult> kdfValidationResults;
        if (MasterPasswordUnlock != null && MasterPasswordAuthentication != null)
        {
            kdfValidationResults = KdfSettingsValidator.Validate(MasterPasswordUnlock.ToData());
        }
        else
        {
            kdfValidationResults = KdfSettingsValidator.Validate(
                Kdf ?? throw new BadRequestException($"{nameof(Kdf)} not found on RequestModel"),
                KdfIterations ?? throw new BadRequestException($"{nameof(KdfIterations)} not found on RequestModel"),
                KdfMemory,
                KdfParallelism);
        }

        return kdfValidationResults;
    }

    // PM-28143 - Remove function
    private static void ThrowIfExistsAndHashIsNotEqual(
        MasterPasswordAuthenticationDataRequestModel? authenticationData,
        string? hash)
    {
        if (authenticationData != null && hash != null)
        {
            if (authenticationData.MasterPasswordAuthenticationHash != hash)
            {
                throw new BadRequestException("Master password hash and hash are not equal.");
            }
        }
    }
}
