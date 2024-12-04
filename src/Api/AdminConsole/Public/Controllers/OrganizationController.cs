using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Enums;
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

    public OrganizationController(
        IOrganizationService organizationService,
        ICurrentContext currentContext,
        GlobalSettings globalSettings)
    {
        _organizationService = organizationService;
        _currentContext = currentContext;
        _globalSettings = globalSettings;
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

        await _organizationService.ImportAsync(
            _currentContext.OrganizationId.Value,
            model.Groups.Select(g => g.ToImportedGroup(_currentContext.OrganizationId.Value)),
            model.Members.Where(u => !u.Deleted).Select(u => u.ToImportedOrganizationUser()),
            model.Members.Where(u => u.Deleted).Select(u => u.ExternalId),
            model.OverwriteExisting.GetValueOrDefault(),
            EventSystemUser.PublicApi);
        return new OkResult();
    }
}
