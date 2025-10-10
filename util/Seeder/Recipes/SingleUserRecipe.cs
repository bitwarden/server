using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;

namespace Bit.Seeder.Recipes;

public class SingleUserRecipe(DatabaseContext db, UserSeeder userSeeder)
{
    public RecipeResult Seed(string email)
    {
        var user = userSeeder.CreateUser(email);

        db.Add(user);
        db.SaveChanges();

        return new RecipeResult
        {
            Result = userSeeder.GetMangleMap(user, new UserData
            {
                Email = email,
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Key = "seeded_key",
                PublicKey = "seeded_public_key",
                PrivateKey = "seeded_private_key",
                ApiKey = "seeded_api_key",
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = 600_000,
            }),
            TrackedEntities = new Dictionary<string, List<Guid>>
            {
                ["User"] = [user.Id]
            }
        };
    }
}
