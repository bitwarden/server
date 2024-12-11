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
    public Guid UserId { get; set; }

    public override bool Valid => Identifier == TokenIdentifier && UserId != default;

    [JsonConstructor]
    public DuoUserStateTokenable() { }

    public DuoUserStateTokenable(User user)
    {
        UserId = user?.Id ?? default;
    }

    public bool TokenIsValid(User user)
    {
        if (UserId == default || user == null)
        {
            return false;
        }

        return UserId == user.Id;
    }
}
