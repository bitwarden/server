using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordHasherService(
    IPasswordHasher<User> passwordHasher,
    IEnumerable<IPasswordValidator<User>> passwordValidators,
    UserManager<User> userManager,
    ILogger<MasterPasswordHasherService> logger) : IMasterPasswordHasher
{
    public async Task<(IdentityResult Result, string? ServerSideHash)> ValidateAndHashPasswordAsync(
        User user, string clientSideHash)
    {
        var errors = new List<IdentityError>();
        foreach (var validator in passwordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, clientSideHash);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("User {UserId} password validation failed: {Errors}.",
                user.Id, string.Join(";", errors.Select(e => e.Code)));
            return (IdentityResult.Failed(errors.ToArray()), null);
        }

        var serverSideHash = passwordHasher.HashPassword(user, clientSideHash);
        return (IdentityResult.Success, serverSideHash);
    }

    public string HashPassword(User user, string clientSideHash)
    {
        return passwordHasher.HashPassword(user, clientSideHash);
    }
}
