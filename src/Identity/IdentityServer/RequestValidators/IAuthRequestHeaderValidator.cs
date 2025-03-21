namespace Bit.Identity.IdentityServer.RequestValidators;

public interface IAuthRequestHeaderValidator
{
    /// <summary>
    /// This method matches the Email in the header the input email. Implementation depends on
    /// GrantValidator.
    /// </summary>
    /// <param name="userEmail">email fetched by grantValidator</param>
    /// <returns>true if the emails match false otherwise</returns>
    bool ValidateAuthEmailHeader(string userEmail);
}