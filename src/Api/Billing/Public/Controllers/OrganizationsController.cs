using System.Net;
using Bit.Api.Models.Public.Response;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationSubscriptionUpdateRequestModel = Bit.Api.Billing.Public.Models.OrganizationSubscriptionUpdateRequestModel;

namespace Bit.Api.Billing.Public.Controllers;

[Route("public/billing-organization")]
[Authorize("Organization")]
public class OrganizationsController : Controller
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;

    public OrganizationsController(
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
    /// Update the organization's current subscription for Password Manager and/or Secrets Manager.
    /// </summary>
    /// <param name="model">The request model containing the updated subscription information.</param>
    [HttpPut("subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PostSubscriptionAsync([FromBody] OrganizationSubscriptionUpdateRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(_currentContext.OrganizationId.Value);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (model.SecretsManagerSubscriptionUpdateRequestModel != null)
        {
            var requestModel = SecretsManagerSubscriptionUpdateRequestModel(model);
            var organizationUpdate = requestModel.ToSecretsManagerSubscriptionUpdate(organization);
            await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(organizationUpdate);
        }

        if (model.PasswordManagerSubscriptionUpdateRequestModel != null)
        {
            await UpdatePasswordManagerSubscriptionAsync(_currentContext.OrganizationId.Value, model);
        }

        return new OkResult();
    }

    private async Task UpdatePasswordManagerSubscriptionAsync(Guid id, OrganizationSubscriptionUpdateRequestModel model)
    {
        await _organizationService.UpdateSubscription(id, model.PasswordManagerSubscriptionUpdateRequestModel.Seats,
            model.PasswordManagerSubscriptionUpdateRequestModel.MaxAutoScaleSeats);
        if (model.PasswordManagerSubscriptionUpdateRequestModel.Storage.HasValue)
        {
            await _organizationService.AdjustStorageAsync(id, (short)model.PasswordManagerSubscriptionUpdateRequestModel.Storage);
        }
    }

    private static SecretsManagerSubscriptionUpdateRequestModel SecretsManagerSubscriptionUpdateRequestModel(
        OrganizationSubscriptionUpdateRequestModel model)
    {
        var requestModel = new SecretsManagerSubscriptionUpdateRequestModel
        {
            SeatAdjustment = model.SecretsManagerSubscriptionUpdateRequestModel.Seats,
            MaxAutoscaleSeats = model.SecretsManagerSubscriptionUpdateRequestModel.MaxAutoScaleSeats,
            ServiceAccountAdjustment = model.SecretsManagerSubscriptionUpdateRequestModel.ServiceAccounts,
            MaxAutoscaleServiceAccounts = model.SecretsManagerSubscriptionUpdateRequestModel.MaxAutoScaleServiceAccounts
        };
        return requestModel;
    }
}
