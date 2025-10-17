using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Factories;

namespace Bit.Seeder.Scenes;

public class SingleUserScene(DatabaseContext db, UserSeeder userSeeder) : IScene<SingleUserScene.Request>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        public bool EmailVerified { get; set; } = false;
        public bool Premium { get; set; } = false;
    }

    public RecipeResult Seed(Request request)
    {
        var user = userSeeder.CreateUser(request.Email, request.EmailVerified, request.Premium);

        db.Add(user);
        db.SaveChanges();

        return new RecipeResult
        {
            Result = userSeeder.GetMangleMap(user, new UserData
            {
                Email = request.Email,
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
