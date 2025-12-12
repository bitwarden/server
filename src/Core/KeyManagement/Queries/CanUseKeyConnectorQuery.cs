using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Queries.Interfaces;

namespace Bit.Core.KeyManagement.Queries;

public class CanUseKeyConnectorQuery : ICanUseKeyConnectorQuery
{
    private readonly ICurrentContext _currentContext;

    public CanUseKeyConnectorQuery(ICurrentContext currentContext)
    {
        _currentContext = currentContext;
    }

    public void VerifyCanUseKeyConnector(User user)
    {
        if (user.UsesKeyConnector)
        {
            throw new BadRequestException("Already uses Key Connector.");
        }

        if (_currentContext.Organizations.Any(u =>
                u.Type is OrganizationUserType.Owner or OrganizationUserType.Admin))
        {
            throw new BadRequestException("Cannot use Key Connector when admin or owner of an organization.");
        }
    }
}
