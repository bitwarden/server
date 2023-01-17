using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Api.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[SecretsManager]
public class SecretsManagerPortingController : Controller
{
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserService _userService;

    public SecretsManagerPortingController(ISecretRepository secretRepository, IProjectRepository projectRepository, IUserService userService)
    {
        _secretRepository = secretRepository;
        _projectRepository = projectRepository;
        _userService = userService;
    }

    [HttpGet("sm/{organizationId}/export")]
    public async Task<SMExportResponseModel> Export([FromRoute] Guid organizationId, [FromRoute] string format = "json")
    {
        var userId = _userService.GetProperUserId(User).Value;
        var projects = await _projectRepository.GetManyByOrganizationIdAsync(organizationId, userId);
        var secrets = await _secretRepository.GetManyByOrganizationIdAsync(organizationId);

        if (secrets == null && projects == null)
        {
            throw new NotFoundException();
        }

        return new SMExportResponseModel(projects, secrets);
    }

    [HttpPost("sm/{organizationId}/import")]
    public async Task<SMExportResponseModel> Import()
    {
        throw new NotImplementedException();
    }
}
