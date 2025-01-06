#nullable enable
using Bit.Core.Entities;
using Bit.Core.Enums;
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

    [StringLength(1000)]
    public required string MasterPasswordHash { get; set; }

    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    public required string UserSymmetricKey { get; set; }

    public required KeysRequestModel UserAsymmetricKeys { get; set; }

    public required KdfType Kdf { get; set; }
    public required int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
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
            Kdf = Kdf,
            KdfIterations = KdfIterations,
            KdfMemory = KdfMemory,
            KdfParallelism = KdfParallelism,
            Key = UserSymmetricKey,
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
        return KdfSettingsValidator.Validate(Kdf, KdfIterations, KdfMemory, KdfParallelism);
    }
}
