using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Models.Api.Request;

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

    public static void ThrowIfExistsAndHashIsNotEqual(
        MasterPasswordAuthenticationDataRequestModel? authenticationData,
        string? hash)
    {
        if (authenticationData != null && hash != null)
        {
            if (authenticationData.MasterPasswordAuthenticationHash != hash)
            {
                throw new Exception("Master password hash and hash are not equal.");
            }
        }
    }
}
