#nullable enable
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;
using System.ComponentModel.DataAnnotations;

//  : IValidatableObject
public class RegisterFinishRequestModel
{
    [Required, StrictEmailAddress, StringLength(256)]
    public string email { get; set; }
    public string emailVerificationToken { get; set; }

    [Required]
    [StringLength(1000)]
    public string MasterPasswordHash { get; set; }

    [StringLength(50)]
    public string MasterPasswordHint { get; set; }


    public string UserSymmetricKey { get; set; }

    public KeysRequestModel UserAsymmetricKeys { get; set; }

    // Leaving these not optional is fine
    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    public Guid? OrganizationUserId { get; set; }
    public string? orgInviteToken { get; set; }


    // TODO: implement a ToUser method but don't worry about Kdf.getValueOrDefault
    // public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    // {
    //     // if (Kdf.HasValue && KdfIterations.HasValue)
    //     // {
    //     //     return KdfSettingsValidator.Validate(Kdf.Value, KdfIterations.Value, KdfMemory, KdfParallelism);
    //     // }
    //     //
    //     // return Enumerable.Empty<ValidationResult>();
    // }
}
