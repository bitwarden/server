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

[Route("public/organizations")]
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
    /// Set Max Seats Autoscale, ServiceAccounts Autoscale, Current Seats,ServiceAccounts and storage for Password Manager and Secrets Manager.
    /// </summary>
    /// <remarks>
    /// Set Max Seats Autoscale,ServiceAccounts Autoscale, Current Seats,ServiceAccounts and storage from an external system.
    /// </remarks>
    /// <param name="id">The identifier of the member to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPut("{id}/subscription")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> PostSubscriptionAsync(Guid id, [FromBody] OrganizationSubscriptionUpdateRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(id);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (model.SecretsManager != null)
        {
            var requestModel = SecretsManagerSubscriptionUpdateRequestModel(model);
            var organizationUpdate = requestModel.ToSecretsManagerSubscriptionUpdate(organization);
            await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(organizationUpdate);
        }

        if (model.PasswordManager != null)
        {
            await UpdatePasswordManagerSubscriptionAsync(id, model);
        }

        return new OkResult();
    }

    private async Task UpdatePasswordManagerSubscriptionAsync(Guid id, OrganizationSubscriptionUpdateRequestModel model)
    {
        await _organizationService.UpdateSubscription(id, model.PasswordManager.Seats,
            model.PasswordManager.MaxAutoScaleSeats);
        if (model.PasswordManager.Storage != 0)
        {
            await _organizationService.AdjustStorageAsync(id, model.PasswordManager.Storage);
        }
    }

    private static SecretsManagerSubscriptionUpdateRequestModel SecretsManagerSubscriptionUpdateRequestModel(
        OrganizationSubscriptionUpdateRequestModel model)
    {
        var requestModel = new SecretsManagerSubscriptionUpdateRequestModel
        {
            SeatAdjustment = model.SecretsManager.Seats,
            MaxAutoscaleSeats = model.SecretsManager.MaxAutoScaleSeats,
            ServiceAccountAdjustment = model.SecretsManager.ServiceAccounts,
            MaxAutoscaleServiceAccounts = model.SecretsManager.MaxAutoScaleServiceAccounts
        };
        return requestModel;
    }
}
