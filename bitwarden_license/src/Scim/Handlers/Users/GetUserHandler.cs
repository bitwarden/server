using System.Net;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Users;
using MediatR;

namespace Bit.Scim.Handlers.Users
{
    public class GetUserHandler : IRequestHandler<GetUserQuery, RequestResult>
    {
        private readonly IOrganizationUserRepository _organizationUserRepository;

        public GetUserHandler(IOrganizationUserRepository organizationUserRepository)
        {
            _organizationUserRepository = organizationUserRepository;
        }

        public async Task<RequestResult> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(request.Id);
            if (orgUser == null || orgUser.OrganizationId != request.OrganizationId)
            {
                return new RequestResult(false, HttpStatusCode.NotFound, new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "User not found."
                });
            }

            return new RequestResult(true, HttpStatusCode.OK, new ScimUserResponseModel(orgUser));
        }
    }
}
