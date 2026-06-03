// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Context;

public class CurrentContextProvider
{
    public CurrentContextProvider() { }

    public CurrentContextProvider(ProviderUser providerUser)
    {
        Id = providerUser.ProviderId;
        Type = providerUser.Type;
        Permissions = string.IsNullOrWhiteSpace(providerUser.Permissions)
            ? new Permissions()
            : JsonSerializer.Deserialize(providerUser.Permissions, AdminConsoleJsonContext.Default.Permissions) ?? new Permissions();
    }

    public Guid Id { get; set; }
    public ProviderUserType Type { get; set; }
    public Permissions Permissions { get; set; }
}
