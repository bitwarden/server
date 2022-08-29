using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class KeysResponseModel : ResponseModel
{
    public KeysResponseModel(User user)
        : base("keys")
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Key = user.Key;
        PublicKey = user.PublicKey;
        PrivateKey = user.PrivateKey;
    }

    public string Key { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKey { get; set; }
}
