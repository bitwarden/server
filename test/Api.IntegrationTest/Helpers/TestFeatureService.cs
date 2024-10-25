using Bit.Core.Services;

namespace Bit.Api.IntegrationTest.Helpers;

public class TestFeatureService(Dictionary<string, object> features) : IFeatureService
{
    public bool IsEnabled(string feature, bool defaultValue = false) => features.TryGetValue(feature, out var value) ? (bool)value : defaultValue;
    public bool IsOnline() => true;
    public int GetIntVariation(string feature, int defaultValue = 0) => features.TryGetValue(feature, out var value) ? (int)value : defaultValue;
    public string GetStringVariation(string feature, string defaultValue = "") => features.TryGetValue(feature, out var value) ? (string)value : defaultValue;
    public Dictionary<string, object> GetAll() => features;
}
