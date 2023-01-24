using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Accounts;

public class SetKeyConnectorKeyRequestModel : IValidatableObject
{
    [Required]
    public string Key { get; set; }
    [Required]
    public KeysRequestModel Keys { get; set; }
    [Required]
    public KdfType Kdf { get; set; }
    [Required]
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
    [Required]
    public string OrgIdentifier { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.Kdf = Kdf;
        existingUser.KdfIterations = KdfIterations;
        existingUser.KdfMemory = KdfMemory;
        existingUser.KdfParallelism = KdfParallelism;
        existingUser.Key = Key;
        Keys.ToUser(existingUser);
        return existingUser;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return KdfSettingsValidator.Validate(Kdf, KdfIterations, KdfMemory, KdfParallelism);
    }
}
