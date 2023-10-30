using System.Net;
using Bit.Api.Billing.Public.Models;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Public.Controllers;

[Route("public/organizationsubscriptions")]
[Authorize("Organization")]
public class OrganizationSubscriptionsController : Controller
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;

    public OrganizationSubscriptionsController(
        IOrganizationService organizationService,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository,
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand)
    {
        _organizationService = organizationService;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
    }

    /// <summary>
    /// Set Max Seats Autoscale and Current Seats for Password Manager.
    /// </summary>
    /// <remarks>
    /// Set Max Seats Autoscale and Current Seats from an external system.
    /// </remarks>
    /// <param name="id">The identifier of the member to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPost("{id}/pm-subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PostSubscription(string id, [FromBody] OrganizationSubscriptionUpdateRequestModel model)
    {
        var orgIdGuid = new Guid(id);
        if (!await _currentContext.EditSubscription(orgIdGuid))
        {
            throw new NotFoundException();
        }
        await _organizationService.UpdateSubscription(orgIdGuid, model.SeatAdjustment, model.MaxAutoscaleSeats);

        return new OkResult();
    }

    /// <summary>
    /// Set Max Seats or ServiceAccounts Autoscale and Current Seats or ServiceAccounts for Secrets Manager.
    /// </summary>
    /// <remarks>
    /// Set Max Seats or ServiceAccounts Autoscale and Current Seats or ServiceAccounts from an external system.
    /// </remarks>
    /// <param name="id">The identifier of the member to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPost("{id}/sm-subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PostSmSubscription(Guid id, [FromBody] SecretsManagerSubscriptionUpdateRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!await _currentContext.EditSubscription(id))
        {
            throw new NotFoundException();
        }

        var organizationUpdate = model.ToSecretsManagerSubscriptionUpdate(organization);
        await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(organizationUpdate);
        return new OkResult();
    }
}
