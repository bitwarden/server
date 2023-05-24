using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response.Providers;

public class ProviderUserResponseModel : ResponseModel
{
    public ProviderUserResponseModel(ProviderUser providerUser, string obj = "providerUser")
        : base(obj)
    {
        if (providerUser == null)
        {
            throw new ArgumentNullException(nameof(providerUser));
        }

        Id = providerUser.Id.ToString();
        UserId = providerUser.UserId?.ToString();
        Type = providerUser.Type;
        Status = providerUser.Status;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(providerUser.Permissions);
    }

    public ProviderUserResponseModel(ProviderUserUserDetails providerUser, string obj = "providerUser")
        : base(obj)
    {
        if (providerUser == null)
        {
            throw new ArgumentNullException(nameof(providerUser));
        }

        Id = providerUser.Id.ToString();
        UserId = providerUser.UserId?.ToString();
        Type = providerUser.Type;
        Status = providerUser.Status;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(providerUser.Permissions);
    }

    public string Id { get; set; }
    public string UserId { get; set; }
    public ProviderUserType Type { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public Permissions Permissions { get; set; }
}

public class ProviderUserUserDetailsResponseModel : ProviderUserResponseModel
{
    public ProviderUserUserDetailsResponseModel(ProviderUserUserDetails providerUser,
        string obj = "providerUserUserDetails") : base(providerUser, obj)
    {
        if (providerUser == null)
        {
            throw new ArgumentNullException(nameof(providerUser));
        }

        Name = providerUser.Name;
        Email = providerUser.Email;
    }

    public string Name { get; set; }
    public string Email { get; set; }
}

public class ProviderUserPublicKeyResponseModel : ResponseModel
{
    public ProviderUserPublicKeyResponseModel(Guid id, Guid userId, string key,
        string obj = "providerUserPublicKeyResponseModel") : base(obj)
    {
        Id = id;
        UserId = userId;
        Key = key;
    }

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; }
}

public class ProviderUserBulkResponseModel : ResponseModel
{
    public ProviderUserBulkResponseModel(Guid id, string error,
        string obj = "providerBulkConfirmResponseModel") : base(obj)
    {
        Id = id;
        Error = error;
    }
    public Guid Id { get; set; }
    public string Error { get; set; }
}
