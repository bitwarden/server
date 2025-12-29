using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// The data used for authentication of a master password.
///
/// This data model does not have any validation, consider using MasterPasswordAuthenticationDataRequestModel
/// if validation is required.
/// </summary>
public class MasterPasswordAuthenticationData
{
    public required KdfSettings Kdf { get; init; }
    public required string MasterPasswordAuthenticationHash { get; init; }
    public required string Salt { get; init; }

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new BadRequestException("Invalid master password salt.");
        }
    }
}
