using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates a standalone user with no organization, registering them as the context owner.
/// </summary>
internal sealed class CreateIndividualUserStep(
    string email, bool premium, short maxStorageGb, bool emailVerified, DateTime? creationDate = null) : IStep
{
    public void Execute(SeederContext context)
    {
        var kdfIterations = context.GetKdfIterations();
        var password = context.GetPassword();

        var (userEntity, keys) = UserSeeder.Create(
            new UserSeed
            {
                Email = email,
                EmailVerified = emailVerified,
                Premium = premium,
                MaxStorageGb = maxStorageGb > 0 ? Math.Min(maxStorageGb, (short)5) : null,
                Password = password,
                KdfIterations = kdfIterations,
                CreationDate = creationDate
            },
            context.GetPasswordHasher(),
            context.GetMangler());

        context.Users.Add(userEntity);
        context.Owner = userEntity;
        context.Domain = email.Split('@')[1];

        context.Registry.UserDigests.Add(
            new EntityRegistry.UserDigest(userEntity.Id, Guid.Empty, keys.Key));
        context.Registry.UserEmailPrefixToUserId[email.Split('@')[0]] = userEntity.Id;
    }
}
