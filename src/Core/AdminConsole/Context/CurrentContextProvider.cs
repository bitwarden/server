using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Context;

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
