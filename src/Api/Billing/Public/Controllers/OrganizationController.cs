using System.Net;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationSubscriptionUpdateRequestModel = Bit.Api.Billing.Public.Models.OrganizationSubscriptionUpdateRequestModel;

namespace Bit.Api.Billing.Public.Controllers;

[Route("public/organization")]
[Authorize("Organization")]
public class OrganizationController : Controller
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;
    private readonly ILogger<OrganizationController> _logger;

    public OrganizationController(
        IOrganizationService organizationService,
        ICurrentContext currentContext,
        IOrganizationRepository organizationRepository,
        IUpdateSecretsManagerSubscriptionCommand updateSecretsManagerSubscriptionCommand,
        ILogger<OrganizationController> logger)
    {
        _organizationService = organizationService;
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
        _updateSecretsManagerSubscriptionCommand = updateSecretsManagerSubscriptionCommand;
        _logger = logger;
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
        try
        {
            await UpdatePasswordManagerAsync(model, _currentContext.OrganizationId.Value);

            var secretsManagerResult = await UpdateSecretsManagerAsync(model, _currentContext.OrganizationId.Value);

            if (!string.IsNullOrEmpty(secretsManagerResult))
            {
                return Ok(new { Message = secretsManagerResult });
            }

            return Ok(new { Message = "Subscription updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while updating the subscription");
            return StatusCode(500, new { Message = "An error occurred while updating the subscription." });
        }
    }

    private async Task UpdatePasswordManagerAsync(OrganizationSubscriptionUpdateRequestModel model, Guid organizationId)
    {
        if (model.PasswordManager != null)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);

            model.PasswordManager.ToPasswordManagerSubscriptionUpdate(organization);
            await _organizationService.UpdateSubscription(organization.Id, (int)model.PasswordManager.Seats,
                model.PasswordManager.MaxAutoScaleSeats);
            if (model.PasswordManager.Storage.HasValue)
            {
                await _organizationService.AdjustStorageAsync(organization.Id, (short)model.PasswordManager.Storage);
            }
        }
    }

    private async Task<string> UpdateSecretsManagerAsync(OrganizationSubscriptionUpdateRequestModel model, Guid organizationId)
    {
        if (model.SecretsManager == null)
        {
            return string.Empty;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        if (!organization.UseSecretsManager)
        {
            return "Organization has no access to Secrets Manager.";
        }

        var secretsManagerUpdate = model.SecretsManager.ToSecretsManagerSubscriptionUpdate(organization);
        await _updateSecretsManagerSubscriptionCommand.UpdateSubscriptionAsync(secretsManagerUpdate);

        return string.Empty;
    }
}
