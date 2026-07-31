using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class RotateUserAccountKeysAndDataRequestModel
{
    [StringLength(300)]
    public required string OldMasterKeyAuthenticationHash { get; set; }
    public required UnlockDataRequestModel AccountUnlockData { get; set; }
    public required AccountKeysRequestModel AccountKeys { get; set; }
    public required AccountDataRequestModel AccountData { get; set; }

    /// <summary>
    /// Optional hex-encoded key id of the new user key this rotation establishes. Absent for clients
    /// that predate the field.
    /// </summary>
    [UserKeyId]
    public string? UserKeyId { get; set; }
}
