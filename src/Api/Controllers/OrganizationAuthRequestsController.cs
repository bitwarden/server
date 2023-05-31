using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("organizations/{orgId}/auth-requests")]
[Authorize("Application")]
public class OrganizationAuthRequestsController : Controller
{
    private readonly IAuthRequestRepository _authRequestRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IAuthRequestService _authRequestService;

    public OrganizationAuthRequestsController(IAuthRequestRepository authRequestRepository,
        ICurrentContext currentContext, IAuthRequestService authRequestService)
    {
        _authRequestRepository = authRequestRepository;
        _currentContext = currentContext;
        _authRequestService = authRequestService;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<PendingOrganizationAuthRequestResponseModel>> GetPendingRequests(Guid orgId)
    {
        if (!await _currentContext.ManageResetPassword(orgId))
        {
            throw new NotFoundException();
        }

        var authRequests = await _authRequestRepository.GetManyPendingByOrganizationIdAsync(orgId);
        var responses = authRequests
            .Select(a => new PendingOrganizationAuthRequestResponseModel(a))
            .ToList();
        return new ListResponseModel<PendingOrganizationAuthRequestResponseModel>(responses);
    }

    [HttpPost("deny")]
    public async Task BulkDenyRequests(Guid orgId, [FromBody] BulkDenyAdminAuthRequestRequestModel model)
    {
        if (!await _currentContext.ManageResetPassword(orgId))
        {
            throw new NotFoundException();
        }

        var authRequests = await _authRequestRepository.GetManyAdminApprovalRequestsByManyIdsAsync(orgId, model.Ids);

        foreach (var authRequest in authRequests)
        {
            await _authRequestService.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId,
                new AuthRequestUpdateRequestModel { RequestApproved = false, });
        }
    }
}
