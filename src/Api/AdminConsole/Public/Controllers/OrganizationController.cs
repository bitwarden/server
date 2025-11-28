// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Public.Controllers;

[Route("public/organization")]
[Authorize("Organization")]
public class OrganizationController : Controller
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;
    private readonly IImportOrganizationUsersAndGroupsCommand _importOrganizationUsersAndGroupsCommand;
    private readonly IFeatureService _featureService;

    public OrganizationController(
        IOrganizationService organizationService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        IImportOrganizationUsersAndGroupsCommand importOrganizationUsersAndGroupsCommand,
        IFeatureService featureService)
    {
        _organizationService = organizationService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
        _importOrganizationUsersAndGroupsCommand = importOrganizationUsersAndGroupsCommand;
        _featureService = featureService;
    }

    /// <summary>
    /// Import members and groups.
    /// </summary>
    /// <remarks>
    /// Import members and groups from an external system.
    /// </remarks>
    /// <param name="model">The request model.</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(OkResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> Import([FromBody] OrganizationImportRequestModel model)
    {
        if (!_globalSettings.SelfHosted && !model.LargeImport &&
            (model.Groups.Count() > 2000 || model.Members.Count(u => !u.Deleted) > 2000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        await _importOrganizationUsersAndGroupsCommand.ImportAsync(
                _currentContext.OrganizationId.Value,
                model.Groups.Select(g => g.ToImportedGroup(_currentContext.OrganizationId.Value)),
                model.Members.Where(u => !u.Deleted).Select(u => u.ToImportedOrganizationUser()),
                model.Members.Where(u => u.Deleted).Select(u => u.ExternalId),
                model.OverwriteExisting.GetValueOrDefault()
                );

        return new OkResult();
    }
}
