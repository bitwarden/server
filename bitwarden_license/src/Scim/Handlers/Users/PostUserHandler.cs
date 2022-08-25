using System.Net;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Scim.Commands.Users;
using Bit.Scim.Context;
using MediatR;

namespace Bit.Scim.Handlers.Users
{
    public class PostUserHandler : IRequestHandler<PostUserCommand, RequestResult>
    {
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IScimContext _scimContext;

        public PostUserHandler(
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IScimContext scimContext)
        {
            _organizationUserRepository = organizationUserRepository;
            _organizationService = organizationService;
            _scimContext = scimContext;
        }

        public async Task<RequestResult> Handle(PostUserCommand request, CancellationToken cancellationToken)
        {
            var email = request.Model.PrimaryEmail?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                switch (_scimContext.RequestScimProvider)
                {
                    case ScimProviderType.AzureAd:
                        email = request.Model.UserName?.ToLowerInvariant();
                        break;
                    default:
                        email = request.Model.WorkEmail?.ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(email))
                        {
                            email = request.Model.Emails?.FirstOrDefault()?.Value?.ToLowerInvariant();
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(email) || !request.Model.Active)
            {
                return new RequestResult(false, HttpStatusCode.BadRequest);
            }

            var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(request.OrganizationId);
            var orgUserByEmail = orgUsers.FirstOrDefault(ou => ou.Email?.ToLowerInvariant() == email);
            if (orgUserByEmail != null)
            {
                return new RequestResult(false, HttpStatusCode.Conflict);
            }

            string externalId = null;
            if (!string.IsNullOrWhiteSpace(request.Model.ExternalId))
            {
                externalId = request.Model.ExternalId;
            }
            else if (!string.IsNullOrWhiteSpace(request.Model.UserName))
            {
                externalId = request.Model.UserName;
            }
            else
            {
                externalId = CoreHelpers.RandomString(15);
            }

            var orgUserByExternalId = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalId);
            if (orgUserByExternalId != null)
            {
                return new RequestResult(false, HttpStatusCode.Conflict);
            }

            var invitedOrgUser = await _organizationService.InviteUserAsync(request.OrganizationId, null, email,
                OrganizationUserType.User, false, externalId, new List<SelectionReadOnly>());
            var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(invitedOrgUser.Id);

            return new RequestResult(true, HttpStatusCode.Created, orgUser);
        }
    }
}
