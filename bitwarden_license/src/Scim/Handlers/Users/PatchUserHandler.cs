using System.Net;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Users;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Handlers.Users
{
    public class PatchUserHandler : IRequestHandler<PatchUserCommand, RequestResult>
    {
        private readonly IUserService _userService;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly ILogger<PatchUserHandler> _logger;

        public PatchUserHandler(
            IUserService userService,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            ILogger<PatchUserHandler> logger)
        {
            _userService = userService;
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _logger = logger;
        }

        public async Task<RequestResult> Handle(PatchUserCommand request, CancellationToken cancellationToken)
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

            var operationHandled = false;
            foreach (var operation in request.Model.Operations)
            {
                // Replace operations
                if (operation.Op?.ToLowerInvariant() == "replace")
                {
                    // Active from path
                    if (operation.Path?.ToLowerInvariant() == "active")
                    {
                        var active = operation.Value.ToString()?.ToLowerInvariant();
                        var handled = await HandleActiveOperationAsync(orgUser, active == "true");
                        if (!operationHandled)
                        {
                            operationHandled = handled;
                        }
                    }
                    // Active from value object
                    else if (string.IsNullOrWhiteSpace(operation.Path) &&
                        operation.Value.TryGetProperty("active", out var activeProperty))
                    {
                        var handled = await HandleActiveOperationAsync(orgUser, activeProperty.GetBoolean());
                        if (!operationHandled)
                        {
                            operationHandled = handled;
                        }
                    }
                }
            }

            if (!operationHandled)
            {
                _logger.LogWarning("User patch operation not handled: {operation} : ",
                    string.Join(", ", request.Model.Operations.Select(o => $"{o.Op}:{o.Path}")));
            }

            return new RequestResult();
        }

        private async Task<bool> HandleActiveOperationAsync(Core.Entities.OrganizationUser orgUser, bool active)
        {
            if (active && orgUser.Status == OrganizationUserStatusType.Revoked)
            {
                await _organizationService.RestoreUserAsync(orgUser, null, _userService);
                return true;
            }
            else if (!active && orgUser.Status != OrganizationUserStatusType.Revoked)
            {
                await _organizationService.RevokeUserAsync(orgUser, null);
                return true;
            }
            return false;
        }
    }
}
