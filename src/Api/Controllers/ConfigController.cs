using Bit.Api.Models.Response;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("config")]
public class ConfigController : Controller
{
    private readonly IGlobalSettings _globalSettings;
    private readonly IFeatureService _featureService;

    public ConfigController(IGlobalSettings globalSettings, IFeatureService featureService)
    {
        _globalSettings = globalSettings;
        _featureService = featureService;
    }

    [HttpGet("")]
    public ConfigResponseModel GetConfigs()
    {
        return new ConfigResponseModel(_globalSettings, _featureService.GetAll());
    }
}
