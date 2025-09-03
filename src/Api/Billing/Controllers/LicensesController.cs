﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api.OrganizationLicenses;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("licenses")]
[Authorize("Licensing")]
[SelfHosted(NotSelfHostedOnly = true)]
public class LicensesController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IGetCloudOrganizationLicenseQuery _getCloudOrganizationLicenseQuery;
    private readonly IValidateBillingSyncKeyCommand _validateBillingSyncKeyCommand;
    private readonly ICurrentContext _currentContext;

    public LicensesController(
        IUserRepository userRepository,
        IUserService userService,
        IOrganizationRepository organizationRepository,
        IGetCloudOrganizationLicenseQuery getCloudOrganizationLicenseQuery,
        IValidateBillingSyncKeyCommand validateBillingSyncKeyCommand,
        ICurrentContext currentContext)
    {
        _userRepository = userRepository;
        _userService = userService;
        _organizationRepository = organizationRepository;
        _getCloudOrganizationLicenseQuery = getCloudOrganizationLicenseQuery;
        _validateBillingSyncKeyCommand = validateBillingSyncKeyCommand;
        _currentContext = currentContext;
    }

    [HttpGet("user/{id}")]
    public async Task<UserLicense> GetUser(string id, [FromQuery] string key)
    {
        var user = await _userRepository.GetByIdAsync(new Guid(id));
        if (user == null)
        {
            return null;
        }
        else if (!user.LicenseKey.Equals(key))
        {
            await Task.Delay(2000);
            throw new BadRequestException("Invalid license key.");
        }

        var license = await _userService.GenerateLicenseAsync(user, null);
        return license;
    }

    /// <summary>
    /// Used by self-hosted installations to get an updated license file
    /// </summary>
    [HttpGet("organization/{id}")]
    public async Task<OrganizationLicense> OrganizationSync(string id, [FromBody] SelfHostedOrganizationLicenseRequestModel model)
    {
        var organization = await _organizationRepository.GetByIdAsync(new Guid(id));
        if (organization == null)
        {
            throw new NotFoundException("Organization not found.");
        }

        if (!organization.LicenseKey.Equals(model.LicenseKey))
        {
            await Task.Delay(2000);
            throw new BadRequestException("Invalid license key.");
        }

        if (!await _validateBillingSyncKeyCommand.ValidateBillingSyncKeyAsync(organization, model.BillingSyncKey))
        {
            throw new BadRequestException("Invalid Billing Sync Key");
        }

        var license = await _getCloudOrganizationLicenseQuery.GetLicenseAsync(organization, _currentContext.InstallationId.Value);
        return license;
    }
}
