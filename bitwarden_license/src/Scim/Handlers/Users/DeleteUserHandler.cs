using System.Net;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Handlers.Users
{
    public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, RequestResult>
    {
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationUserRepository _organizationUserRepository;

        public DeleteUserHandler(
            IOrganizationService organizationService,
            IOrganizationUserRepository organizationUserRepository)
        {
            _organizationService = organizationService;
            _organizationUserRepository = organizationUserRepository;
        }

        public async Task<RequestResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(request.Id);
            if (orgUser == null || orgUser.OrganizationId != request.OrganizationId)
            {
                return new RequestResult(false, HttpStatusCode.NotFound, new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "User not found."
                });
            }
            await _organizationService.DeleteUserAsync(request.OrganizationId, request.Id, null);

            return new RequestResult();
        }
    }
}
