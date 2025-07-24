using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.KeyManagement.Models.Requests;

public class MasterPasswordAuthenticationDataRequestModel
{
    public required InnerKdfRequestModel Kdf { get; init; }
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
