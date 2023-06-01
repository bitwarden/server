using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    private readonly IFeatureService _featureService;

    public OrganizationAuthRequestsController(IAuthRequestRepository authRequestRepository,
        ICurrentContext currentContext, IAuthRequestService authRequestService, IFeatureService featureService)
    {
        _authRequestRepository = authRequestRepository;
        _currentContext = currentContext;
        _authRequestService = authRequestService;
        _featureService = featureService;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<PendingOrganizationAuthRequestResponseModel>> GetPendingRequests(Guid orgId)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext))
        {
            throw new NotFoundException();
        }

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

    [HttpPost("{requestId}")]
    public async Task UpdateAuthRequest(Guid orgId, Guid requestId, [FromBody] AdminAuthRequestUpdateRequestModel model)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext))
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.ManageResetPassword(orgId))
        {
            throw new NotFoundException();
        }

        var authRequest =
            (await _authRequestRepository.GetManyAdminApprovalRequestsByManyIdsAsync(orgId, new[] { requestId })).FirstOrDefault();

        if (authRequest == null || authRequest.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        await _authRequestService.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId,
            new AuthRequestUpdateRequestModel { RequestApproved = model.RequestApproved, Key = model.Key });
    }

    [HttpPost("deny")]
    public async Task BulkDenyRequests(Guid orgId, [FromBody] BulkDenyAdminAuthRequestRequestModel model)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.TrustedDeviceEncryption, _currentContext))
        {
            throw new NotFoundException();
        }

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
