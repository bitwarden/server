using Bit.Core.Entities;
using Bit.Core.KeyManagement.PasswordHashing;
using Microsoft.AspNetCore.Identity;

namespace Microsoft.Extensions.DependencyInjection;

public static class UserPasswordHasherServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPasswordHasher{User}"/> backed by the Key Management password hashing package.
    /// </summary>
    public static void AddUserPasswordHasher(this IServiceCollection services)
    {
        services.AddServerPasswordHasher();

        // Not TryAdd: Identity registers its own PasswordHasher<User> with TryAdd, and this must win
        // whichever runs first.
        services.AddSingleton<IPasswordHasher<User>, UserPasswordHasher>();
    }
}
