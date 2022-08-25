using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users;
using MediatR;

namespace Bit.Scim.Handlers.Users
{
    public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
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

        public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var orgUser = await _organizationUserRepository.GetByIdAsync(request.Id);
            if (orgUser == null || orgUser.OrganizationId != request.OrganizationId)
            {
                throw new NotFoundException("User not found.");
            }

            await _organizationService.DeleteUserAsync(request.OrganizationId, request.Id, null);

            return Unit.Value;
        }
    }
}
