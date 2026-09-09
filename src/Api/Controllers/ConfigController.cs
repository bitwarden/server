using Bit.Api.Models.Response;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Environment;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("config")]
public class ConfigController : Controller
{
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;
    private readonly IBitwardenEnvironment _bitwardenEnvironment;

    public ConfigController(
        IGlobalSettings globalSettings,
        IFeatureService featureService,
        IBitwardenEnvironment bitwardenEnvironment)
    {
        _globalSettings = globalSettings;
        _featureService = featureService;
        _bitwardenEnvironment = bitwardenEnvironment;
    }

    [HttpGet("")]
    public ConfigResponseModel GetConfigs()
    {
        return new ConfigResponseModel(_featureService, _globalSettings, _bitwardenEnvironment);
    }
}
