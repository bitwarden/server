using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Models.Api.Request;

/// <summary>
/// Use this datatype when interfacing with requests to create a separation of concern.
/// See <see cref="MasterPasswordAuthenticationData"/> to
/// </summary>
public class MasterPasswordAuthenticationDataRequestModel
{
    public required KdfRequestModel Kdf { get; init; }
    public required string MasterPasswordAuthenticationHash { get; init; }
    [StringLength(256)] public required string Salt { get; init; }

    public MasterPasswordAuthenticationData ToData()
    {
        return new MasterPasswordAuthenticationData
        {
            Kdf = Kdf.ToData(),
            MasterPasswordAuthenticationHash = MasterPasswordAuthenticationHash,
            Salt = Salt
        };
    }
}
