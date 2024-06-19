using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.Registration;

public interface IRegisterUserCommand
{
    public Task<IdentityResult> RegisterUser(User user, string masterPasswordHash, string orgInviteToken, Guid? orgUserId);

    // public Task<IdentityResult> RegisterUserViaEmailVerificationTokenAsync(User user, string masterPasswordHash, string emailVerificationToken);

}
