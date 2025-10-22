﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.Models.Public.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Public.Controllers;

[Route("public/policies")]
[Authorize("Organization")]
public class PoliciesController : Controller
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyService _policyService;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private readonly ISavePolicyCommand _savePolicyCommand;
    private readonly IVNextSavePolicyCommand _vNextSavePolicyCommand;

    public PoliciesController(
        IPolicyRepository policyRepository,
        IPolicyService policyService,
        ICurrentContext currentContext,
        IFeatureService featureService,
        ISavePolicyCommand savePolicyCommand,
        IVNextSavePolicyCommand vNextSavePolicyCommand)
    {
        _policyRepository = policyRepository;
        _policyService = policyService;
        _currentContext = currentContext;
        _featureService = featureService;
        _savePolicyCommand = savePolicyCommand;
        _vNextSavePolicyCommand = vNextSavePolicyCommand;
    }

    /// <summary>
    /// Retrieve a policy.
    /// </summary>
    /// <remarks>
    /// Retrieves the details of a policy.
    /// </remarks>
    /// <param name="type">The type of policy to be retrieved.</param>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(PolicyResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Get(PolicyType type)
    {
        var policy = await _policyRepository.GetByOrganizationIdTypeAsync(_currentContext.OrganizationId.Value, type);
        if (policy == null)
        {
            return new NotFoundResult();
        }

        return new JsonResult(new PolicyResponseModel(policy));
    }

    /// <summary>
    /// List all policies.
    /// </summary>
    /// <remarks>
    /// Returns a list of your organization's policies.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ListResponseModel<PolicyResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> List()
    {
        var policies = await _policyRepository.GetManyByOrganizationIdAsync(_currentContext.OrganizationId.Value);

        return new JsonResult(new ListResponseModel<PolicyResponseModel>(policies.Select(p => new PolicyResponseModel(p))));
    }

    /// <summary>
    /// Update a policy.
    /// </summary>
    /// <remarks>
    /// Updates the specified policy. If a property is not provided,
    /// the value of the existing property will be reset.
    /// </remarks>
    /// <param name="type">The type of policy to be updated.</param>
    /// <param name="model">The request model.</param>
    [HttpPut("{type}")]
    [ProducesResponseType(typeof(PolicyResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> Put(PolicyType type, [FromBody] PolicyUpdateRequestModel model)
    {
        Policy policy;
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyValidatorsRefactor))
        {
            var savePolicyModel = model.ToSavePolicyModel(_currentContext.OrganizationId!.Value, type);
            policy = await _vNextSavePolicyCommand.SaveAsync(savePolicyModel);
        }
        else
        {
            var policyUpdate = model.ToPolicyUpdate(_currentContext.OrganizationId!.Value, type);
            policy = await _savePolicyCommand.SaveAsync(policyUpdate);
        }

        var response = new PolicyResponseModel(policy);
        return new JsonResult(response);
    }
}
