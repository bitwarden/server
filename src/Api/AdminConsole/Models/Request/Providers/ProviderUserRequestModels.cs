using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Providers;

public class ProviderUserInviteRequestModel
{
    [Required]
    [StrictEmailAddressList]
    public IEnumerable<string> Emails { get; set; }

    [Required]
    public ProviderUserType? Type { get; set; }
}

public class ProviderUserAcceptRequestModel
{
    [Required]
    public string Token { get; set; }
}

public class ProviderUserConfirmRequestModel
{
    [Required]
    public string Key { get; set; }
}

public class ProviderUserBulkConfirmRequestModelEntry
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Key { get; set; }
}

public class ProviderUserBulkConfirmRequestModel
{
    [Required]
    public IEnumerable<ProviderUserBulkConfirmRequestModelEntry> Keys { get; set; }

    public Dictionary<Guid, string> ToDictionary()
    {
        return Keys.ToDictionary(e => e.Id, e => e.Key);
    }
}

public class ProviderUserUpdateRequestModel
{
    [Required]
    public ProviderUserType? Type { get; set; }

    public ProviderUser ToProviderUser(ProviderUser existingUser)
    {
        existingUser.Type = Type.Value;
        return existingUser;
    }
}

public class ProviderUserBulkRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}
