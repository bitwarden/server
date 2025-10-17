using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Tokens;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Seeder.Recipes;

public class EmergencyAccessInviteRecipe(
    DatabaseContext db,
    IDataProtectorTokenFactory<EmergencyAccessInviteTokenable> dataProtectorTokenizer)
{
    public RecipeResult Seed(string email)
    {
        var invites = db.EmergencyAccesses
            .Where(ea => ea.Email == email).ToList().Select(ea =>
            {
                var token = dataProtectorTokenizer.Protect(
                    new EmergencyAccessInviteTokenable(ea, hoursTillExpiration: 1)
                );
                return $"/accept-emergency?id={ea.Id}&name=Dummy&email={ea.Email}&token={token}";
            });

        return new RecipeResult
        {
            Result = invites,
        };
    }
}
