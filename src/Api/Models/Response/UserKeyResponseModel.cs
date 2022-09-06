using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class UserKeyResponseModel : ResponseModel
{
    public UserKeyResponseModel(Guid id, string key)
        : base("userKey")
    {
        UserId = id.ToString();
        PublicKey = key;
    }

    public string UserId { get; set; }
    public string PublicKey { get; set; }
}
