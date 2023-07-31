using System.Net;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.DirectoryConnector.Interfaces;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Public.Controllers;

[Route("public/organization")]
[Authorize("Organization")]
public class OrganizationController : Controller
{
    private readonly IDirectoryConnectorSyncCommand _directoryConnectorSyncCommand;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    public OrganizationController(
        IDirectoryConnectorSyncCommand directoryConnectorSyncCommand,
        ICurrentContext currentContext,
        GlobalSettings globalSettings)
    {
        _directoryConnectorSyncCommand = directoryConnectorSyncCommand;
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
    [ProducesResponseType(typeof(MemberResponseModel), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponseModel), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> Import([FromBody] OrganizationImportRequestModel model)
    {
        if (!_globalSettings.SelfHosted && !model.LargeImport &&
            (model.Groups.Count() > 2000 || model.Members.Count(u => !u.Deleted) > 2000))
        {
            throw new BadRequestException("You cannot import this much data at once.");
        }

        await _directoryConnectorSyncCommand.SyncOrganizationAsync(
            _currentContext.OrganizationId.Value,
            null,
            model.Groups.Select(g => g.ToImportedGroup(_currentContext.OrganizationId.Value)),
            model.Members.Where(u => !u.Deleted).Select(u => u.ToImportedOrganizationUser()),
            model.Members.Where(u => u.Deleted).Select(u => u.ExternalId),
            model.OverwriteExisting.GetValueOrDefault());
        return new OkResult();
    }
}
