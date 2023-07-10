using System.Reflection;
using AutoFixture;
using Bit.Test.Common.Helpers;
using Xunit.Sdk;

namespace Bit.Test.Common.AutoFixture.Attributes;

[DataDiscoverer("AutoFixture.Xunit2.NoPreDiscoveryDataDiscoverer", "AutoFixture.Xunit2")]
public class BitAutoDataAttribute : DataAttribute
{
    private readonly Func<IFixture> _fixtureFactory;
    private readonly object[] _fixedTestParameters;
    private IFixture _fixture() => WithAuthNSubstitutions ? _fixtureFactory().WithAutoNSubstitutions() : _fixtureFactory();

    public bool WithAuthNSubstitutions { get; set; } = true;

    public BitAutoDataAttribute(params object[] fixedTestParameters) :
        this(() => new Fixture(), fixedTestParameters)
    { }

    public BitAutoDataAttribute(Func<IFixture> createFixture, params object[] fixedTestParameters) :
        base()
    {
        _fixtureFactory = createFixture;
        _fixedTestParameters = fixedTestParameters;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        => BitAutoDataAttributeHelpers.GetData(testMethod, _fixture(), _fixedTestParameters);
}
