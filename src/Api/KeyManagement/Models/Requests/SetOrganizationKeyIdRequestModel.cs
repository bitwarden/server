using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class SetOrganizationKeyIdRequestModel
{
    /// <summary>
    /// Hex-encoded key id of the organization's current organization key.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [KeyId]
    public required string OrganizationKeyId { get; init; }

    public KeyId ToKeyId() => KeyId.FromHexEncodedString(OrganizationKeyId)!;
}
