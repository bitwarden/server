namespace Bit.Core.Services.Implementations;

public class DelegatingFeatureService : IFeatureService
{
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;

    public DelegatingFeatureService(
        Bitwarden.Server.Sdk.Features.IFeatureService featureService)
    {
        _featureService = featureService;
    }

    public bool IsEnabled(string key, bool defaultValue = false)
    {
        return _featureService.IsEnabled(key, defaultValue);
    }

    public int GetIntVariation(string key, int defaultValue = 0)
    {
        return _featureService.GetIntVariation(key, defaultValue);
    }

    public string? GetStringVariation(string key, string? defaultValue = null)
    {
        return _featureService.GetStringVariation(key, defaultValue);
    }
}
