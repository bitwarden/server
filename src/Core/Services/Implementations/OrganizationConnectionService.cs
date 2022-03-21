using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class OrganizationConnectionService : IOrganizationConnectionService
    {
        private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
        private readonly IOrganizationConnectionValidatorFactory _organizationConnectionValidatorFactory;

        public OrganizationConnectionService(IOrganizationConnectionRepository organizationConnectionRepository,
            IOrganizationConnectionValidatorFactory organizationConnectionValidatorFactory)
        {
            _organizationConnectionRepository = organizationConnectionRepository;
            _organizationConnectionValidatorFactory = organizationConnectionValidatorFactory;
        }

        public async Task SaveAsync(OrganizationConnection organizationConnection)
        {
            // Validate config
            var validator = _organizationConnectionValidatorFactory.GetValidator(organizationConnection.Type);

            // The validator is allowed to and encouraged to update the config so that bad JSON isn't getting stored
            // so we need to update our copy with what is given.
            organizationConnection = await validator.ValidateAsync(organizationConnection);

            await _organizationConnectionRepository.UpsertAsync(organizationConnection);
        }
    }
}
