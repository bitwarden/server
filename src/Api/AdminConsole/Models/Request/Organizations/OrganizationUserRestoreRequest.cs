using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUserRestoreRequest
{
    /// <summary>
    /// This is the encrypted default collection name to be used for restored users if required
    /// </summary>
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? DefaultUserCollectionName { get; set; }
}
