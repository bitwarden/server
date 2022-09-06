using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Users;
using MediatR;

namespace Bit.Scim.Handlers.Users;

public class GetUserHandler : IRequestHandler<GetUserQuery, ScimUserResponseModel>
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetUserHandler(IOrganizationUserRepository organizationUserRepository)
    {
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<ScimUserResponseModel> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(request.Id);
        if (orgUser == null || orgUser.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("User not found.");
        }

        return new ScimUserResponseModel(orgUser);
    }
}
