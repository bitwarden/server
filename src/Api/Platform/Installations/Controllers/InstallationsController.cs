using Bit.Core.Exceptions;
using Bit.Core.Platform.Installations;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Platform.Installations;

/// <summary>
/// Routes used to manipulate `Installation` objects: a type used to manage
/// a record of a self hosted installation.
/// </summary>
/// <remarks>
/// This controller is not called from any clients. It's primarily referenced
/// in the `Setup` project for creating a new self hosted installation.
/// </remarks>
/// <seealso>Bit.Setup.Program</seealso>
[Route("installations")]
[SelfHosted(NotSelfHostedOnly = true)]
public class InstallationsController : Controller
{
    private readonly IInstallationRepository _installationRepository;

    public InstallationsController(
        IInstallationRepository installationRepository)
    {
        _installationRepository = installationRepository;
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<InstallationResponseModel> Get(Guid id)
    {
        var installation = await _installationRepository.GetByIdAsync(id);
        if (installation == null)
        {
            throw new NotFoundException();
        }

        return new InstallationResponseModel(installation, false);
    }

    [HttpPost("")]
    [AllowAnonymous]
    public async Task<InstallationResponseModel> Post([FromBody] InstallationRequestModel model)
    {
        var installation = model.ToInstallation();
        await _installationRepository.CreateAsync(installation);
        return new InstallationResponseModel(installation, true);
    }
}
