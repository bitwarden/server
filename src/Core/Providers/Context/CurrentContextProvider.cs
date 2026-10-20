// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data;
using Bit.Core.Providers.Entities;
using Bit.Core.Providers.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Providers.Context;

public class CurrentContextProvider
{
    public CurrentContextProvider() { }

    public CurrentContextProvider(ProviderUser providerUser)
    {
        Id = providerUser.ProviderId;
        Type = providerUser.Type;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(providerUser.Permissions);
    }

    public Guid Id { get; set; }
    public ProviderUserType Type { get; set; }
    public Permissions Permissions { get; set; }
}
