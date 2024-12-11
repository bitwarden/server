#nullable enable

using System.Reflection;
using AutoFixture;
using Bit.Test.Common.Helpers;
using Xunit.Sdk;

namespace Bit.Test.Common.AutoFixture.Attributes;

[DataDiscoverer("AutoFixture.Xunit2.NoPreDiscoveryDataDiscoverer", "AutoFixture.Xunit2")]
public class BitAutoDataAttribute : DataAttribute
{
    private readonly Func<IFixture> _createFixture;
    private readonly object?[] _fixedTestParameters;

    public BitAutoDataAttribute()
        : this(Array.Empty<object>()) { }

    public BitAutoDataAttribute(params object?[] fixedTestParameters)
        : this(() => new Fixture(), fixedTestParameters) { }

    public BitAutoDataAttribute(Func<IFixture> createFixture, params object?[] fixedTestParameters)
        : base()
    {
        _createFixture = createFixture;
        _fixedTestParameters = fixedTestParameters;
    }

    public override IEnumerable<object?[]> GetData(MethodInfo testMethod) =>
        BitAutoDataAttributeHelpers.GetData(testMethod, _createFixture(), _fixedTestParameters);
}
