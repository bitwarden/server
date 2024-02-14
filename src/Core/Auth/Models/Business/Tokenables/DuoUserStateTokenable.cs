using Bit.Core.Entities;
using Bit.Core.Tokens;
using Newtonsoft.Json;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class DuoUserStateTokenable : Tokenable
{
    public const string ClearTextPrefix = "BwDuoUserId";
    public const string DataProtectorPurpose = "DuoUserIdTokenDataProtector";
    public const string TokenIdentifier = "DuoUserIdToken";
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }

    public override bool Valid => Identifier == TokenIdentifier &&
                                  Id != default;

    [JsonConstructor]
    public DuoUserStateTokenable()
    {
    }

    public DuoUserStateTokenable(User user)
    {
        Id = user?.Id ?? default;
    }

    public bool TokenIsValid(User user)
    {
        if (Id == default || user == null)
        {
            return false;
        }

        return Id == user.Id;
    }
}
