using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class SetUserKeyIdRequestModel
{
    /// <summary>
    /// Hex-encoded key id of the user's current user key.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [UserKeyId]
    public required string UserKeyId { get; init; }

    // UserKeyId is required and non-empty, so the parse never returns null here.
    public KeyId ToKeyId() => KeyId.FromHexEncodedString(UserKeyId)!;
}
