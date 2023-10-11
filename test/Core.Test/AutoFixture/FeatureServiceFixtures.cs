using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;

namespace Bit.Core.Test.AutoFixture;

internal class FeatureServiceBuilder : ISpecimenBuilder
{
    private readonly string _enabledFeatureFlag;

    public FeatureServiceBuilder(string enabledFeatureFlag)
    {
        _enabledFeatureFlag = enabledFeatureFlag;
    }

    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (request is not ParameterInfo pi)
        {
            return new NoSpecimen();
        }

        if (pi.ParameterType == typeof(IFeatureService))
        {
            var featureService = Substitute.For<IFeatureService>();
            featureService
                .IsEnabled(_enabledFeatureFlag, Arg.Any<ICurrentContext>(), Arg.Any<bool>())
                .Returns(true);
            return featureService;
        }

        return new NoSpecimen();
    }
}

internal class FeatureServiceCustomization : ICustomization
{
    private readonly string _enabledFeatureFlag;

    public FeatureServiceCustomization(string enabledFeatureFlag)
    {
        _enabledFeatureFlag = enabledFeatureFlag;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new FeatureServiceBuilder(_enabledFeatureFlag));
    }
}

/// <summary>
/// Arranges the IFeatureService mock to enable the specified feature flag
/// </summary>
public class FeatureServiceCustomizeAttribute : BitCustomizeAttribute
{
    private readonly string _enabledFeatureFlag;

    public FeatureServiceCustomizeAttribute(string enabledFeatureFlag)
    {
        _enabledFeatureFlag = enabledFeatureFlag;
    }

    public override ICustomization GetCustomization() => new FeatureServiceCustomization(_enabledFeatureFlag);
}
