using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class RequireFeatureAttributeTests
{
    private const string _testFeature = "test-feature";

    [Fact]
    public void Throws_When_Feature_Disabled()
    {
        // Arrange
        var rfa = new RequireFeatureAttribute(_testFeature);

        // Act & Assert
        Assert.Throws<FeatureUnavailableException>(() => rfa.OnActionExecuting(GetContext(enabled: false)));
    }

    [Fact]
    public void Throws_When_Feature_Not_Found()
    {
        // Arrange
        var rfa = new RequireFeatureAttribute("missing-feature");

        // Act & Assert
        Assert.Throws<FeatureUnavailableException>(() => rfa.OnActionExecuting(GetContext(enabled: false)));
    }

    [Fact]
    public void Success_When_Feature_Enabled()
    {
        // Arrange
        var rfa = new RequireFeatureAttribute(_testFeature);

        // Act
        rfa.OnActionExecuting(GetContext(enabled: true));

        // Assert
        // The Assert here is NOT throwing an exception
    }


    /// <summary>
    /// Generates a ActionExecutingContext with the necessary services registered to test
    /// the <see cref="RequireFeatureAttribute"/>
    /// </summary>
    /// <param name="enabled">Mock value for the <see cref="_testFeature"/> flag</param>
    /// <returns></returns>
    private static ActionExecutingContext GetContext(bool enabled)
    {
        IServiceCollection services = new ServiceCollection();

        var featureService = Substitute.For<IFeatureService>();
        var currentContext = Substitute.For<ICurrentContext>();

        featureService.IsEnabled(_testFeature, Arg.Any<ICurrentContext>()).Returns(enabled);

        services.AddSingleton(featureService);
        services.AddSingleton(currentContext);

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = services.BuildServiceProvider();

        var context = Substitute.For<ActionExecutingContext>(
            Substitute.For<ActionContext>(httpContext,
                new RouteData(),
                Substitute.For<ActionDescriptor>()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object>(),
            Substitute.For<Controller>());

        return context;
    }
}
