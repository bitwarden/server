using Bit.Admin.Models;
using Bit.Core.Entities.Provider;

namespace Bit.Admin.Providers.Interfaces;

public interface ICreateProviderCommand
{
    Task<Provider> Create(CreateProviderModel model);
}
