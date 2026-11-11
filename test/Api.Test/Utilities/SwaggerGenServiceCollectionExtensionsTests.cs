using Bit.Api.Utilities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class SwaggerGenServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSwaggerGen_ExcludesGovCloudRegionFromServers()
    {
        var options = BuildSwaggerGenOptions();
        var govRegion = CloudRegionConfig.FindByRegion(CloudRegion.Gov);

        Assert.DoesNotContain(options.SwaggerGeneratorOptions.Servers, s => s.Url == govRegion.ApiUrl);
        Assert.DoesNotContain(options.SwaggerGeneratorOptions.SecuritySchemes.Keys, k => k == $"{CloudRegion.Gov}_server");
    }

    [Theory]
    [InlineData(CloudRegion.US)]
    [InlineData(CloudRegion.EU)]
    public void AddSwaggerGen_IncludesPublicCloudRegionServers(CloudRegion region)
    {
        var options = BuildSwaggerGenOptions();
        var regionConfig = CloudRegionConfig.FindByRegion(region);

        Assert.Contains(options.SwaggerGeneratorOptions.Servers, s => s.Url == regionConfig.ApiUrl);
        Assert.Contains($"{region}_server", options.SwaggerGeneratorOptions.SecuritySchemes.Keys);
    }

    private static SwaggerGenOptions BuildSwaggerGenOptions()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Production");

        var services = new ServiceCollection();
        services.AddSwaggerGen(new GlobalSettings(), environment);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
    }
}
