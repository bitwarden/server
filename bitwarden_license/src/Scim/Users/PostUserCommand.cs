using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;

namespace Bit.Scim.Users;

public class PostUserCommand : IPostUserCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IScimContext _scimContext;

    public PostUserCommand(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IScimContext scimContext)
    {
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _scimContext = scimContext;
    }

    public async Task<OrganizationUserUserDetails> PostUserAsync(Guid organizationId, ScimUserRequestModel model)
    {
        var email = model.PrimaryEmail?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            switch (_scimContext.RequestScimProvider)
            {
                case ScimProviderType.AzureAd:
                    email = model.UserName?.ToLowerInvariant();
                    break;
                default:
                    email = model.WorkEmail?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        email = model.Emails?.FirstOrDefault()?.Value?.ToLowerInvariant();
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(email) || !model.Active)
        {
            throw new BadRequestException();
        }

        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var orgUserByEmail = orgUsers.FirstOrDefault(ou => ou.Email?.ToLowerInvariant() == email);
        if (orgUserByEmail != null)
        {
            throw new ConflictException();
        }

        string externalId = null;
        if (!string.IsNullOrWhiteSpace(model.ExternalId))
        {
            externalId = model.ExternalId;
        }
        else if (!string.IsNullOrWhiteSpace(model.UserName))
        {
            externalId = model.UserName;
        }
        else
        {
            externalId = CoreHelpers.RandomString(15);
        }

        var orgUserByExternalId = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalId);
        if (orgUserByExternalId != null)
        {
            throw new ConflictException();
        }

        var invitedOrgUser = await _organizationService.InviteUserAsync(organizationId, EventSystemUser.SCIM, email,
            OrganizationUserType.User, false, externalId, new List<SelectionReadOnly>());
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(invitedOrgUser.Id);

        return orgUser;
    }
}
