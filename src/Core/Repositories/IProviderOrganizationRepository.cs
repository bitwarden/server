using System;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Repositories
{
    public interface IProviderOrganizationRepository : IRepository<Provider, Guid>
    {
<<<<<<< HEAD
=======
        Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId);
        Task<ProviderOrganization> GetByOrganizationId(Guid organizationId);
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
    }
}
