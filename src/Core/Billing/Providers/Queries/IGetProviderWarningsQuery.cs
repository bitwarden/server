using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Providers.Models;

namespace Bit.Core.Billing.Providers.Queries;

public interface IGetProviderWarningsQuery
{
    Task<ProviderWarnings?> Run(Provider provider);
}
