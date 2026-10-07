using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.Queries;

public class KeyConnectorConfirmationDetailsQuery : IKeyConnectorConfirmationDetailsQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public KeyConnectorConfirmationDetailsQuery(IOrganizationRepository organizationRepository, IOrganizationUserRepository organizationUserRepository)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<KeyConnectorConfirmationDetails> Run(string orgSsoIdentifier, Guid userId)
    {
        var org = await _organizationRepository.GetByIdentifierAsync(orgSsoIdentifier);
        if (org is not { UseKeyConnector: true })
        {
            throw new NotFoundException();
        }

        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(org.Id, userId);
        if (orgUser == null)
        {
            throw new NotFoundException();
        }

        return new KeyConnectorConfirmationDetails { OrganizationName = org.Name, };
    }
}
