using System.Text.Json;
using Bit.Api.Models.Response;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class EnvironmentConfigResponseModelTests
{
    [Fact]
    public void Serialize_FillAssistRulesNull_OmitsPropertyFromJson()
    {
        var model = new EnvironmentConfigResponseModel
        {
            CloudRegion = "US",
            Vault = "https://vault.bitwarden.com",
            FillAssistRules = null
        };

        var json = JsonSerializer.Serialize(model);

        Assert.DoesNotContain("FillAssistRules", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_FillAssistRulesSet_IncludesPropertyInJson()
    {
        var expectedUri = "https://example.com/rules.json";
        var model = new EnvironmentConfigResponseModel
        {
            CloudRegion = "US",
            Vault = "https://vault.bitwarden.com",
            FillAssistRules = expectedUri
        };

        var json = JsonSerializer.Serialize(model);

        Assert.Contains("FillAssistRules", json);
        Assert.Contains(expectedUri, json);
    }
}
