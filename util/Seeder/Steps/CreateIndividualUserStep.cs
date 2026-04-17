using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates a standalone user with no organization, registering them as the context owner.
/// </summary>
internal sealed class CreateIndividualUserStep(string email, bool premium, short maxStorageGb) : IStep
{
    public void Execute(SeederContext context)
    {
        var kdfIterations = context.GetKdfIterations();
        var password = context.GetPassword();

        var (userEntity, keys) = UserSeeder.Create(
            email,
            context.GetPasswordHasher(),
            context.GetMangler(),
            premium: premium,
            maxStorageGb: maxStorageGb > 0 ? Math.Min(maxStorageGb, (short)5) : null,
            password: password,
            kdfIterations: kdfIterations);

        if (premium)
        {
            userEntity.PremiumExpirationDate = DateTime.UtcNow.AddYears(1);
        }

        context.Users.Add(userEntity);
        context.Owner = userEntity;
        context.Domain = email.Split('@')[1];

        context.Registry.UserDigests.Add(
            new EntityRegistry.UserDigest(userEntity.Id, Guid.Empty, keys.Key));
        context.Registry.UserEmailPrefixToUserId[email.Split('@')[0]] = userEntity.Id;
    }
}
