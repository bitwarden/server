using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("licenses")]
[Authorize("Licensing")]
[SelfHosted(NotSelfHostedOnly = true)]
public class LicensesController : Controller
{
    private readonly ILicensingService _licensingService;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;

    public LicensesController(
        ILicensingService licensingService,
        IUserRepository userRepository,
        IUserService userService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        ICurrentContext currentContext)
    {
        _licensingService = licensingService;
        _userRepository = userRepository;
        _userService = userService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
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

    [HttpGet("organization/{id}")]
    public async Task<OrganizationLicense> GetOrganization(string id, [FromQuery] string key)
    {
        var org = await _organizationRepository.GetByIdAsync(new Guid(id));
        if (org == null)
        {
            return null;
        }
        else if (!org.LicenseKey.Equals(key))
        {
            await Task.Delay(2000);
            throw new BadRequestException("Invalid license key.");
        }

        var license = await _organizationService.GenerateLicenseAsync(org, _currentContext.InstallationId.Value);
        return license;
    }
}
