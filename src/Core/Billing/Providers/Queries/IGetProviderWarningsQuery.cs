using Bit.Core.Billing.Providers.Models;
using Bit.Core.Providers.Entities;

namespace Bit.Core.Billing.Providers.Queries;

public interface IGetProviderWarningsQuery
{
    Task<ProviderWarnings?> Run(Provider provider);
}
