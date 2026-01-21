namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUserRestoreRequest
{
    /// <summary>
    /// This is the encrypted default collection name to be used for restored users if required
    /// </summary>
    public string DefaultUserCollectionName { get; set; } = string.Empty;
}
