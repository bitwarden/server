using System.Reflection;
using AutoFixture;
using Bit.Test.Common.Helpers;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class BitAutoDataAttribute : DataAttribute
{
    private readonly Func<IFixture> _createFixture;
    private readonly object?[] _fixedTestParameters;

    public BitAutoDataAttribute() : this(Array.Empty<object>()) { }

    public BitAutoDataAttribute(params object?[] fixedTestParameters) :
        this(() => new Fixture(), fixedTestParameters)
    { }

    public BitAutoDataAttribute(Func<IFixture> createFixture, params object?[] fixedTestParameters) :
        base()
    {
        _createFixture = createFixture;
        _fixedTestParameters = fixedTestParameters;
    }

    protected IEnumerable<object?[]> GetDataCore(MethodInfo testMethod)
    {
        return BitAutoDataAttributeHelpers.GetData(testMethod, _createFixture(), _fixedTestParameters);
    }

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var theoryData = new List<ITheoryDataRow>();
        var data = GetDataCore(testMethod);

        foreach (var dataRow in data)
        {
            theoryData.Add(new TheoryDataRow(dataRow));
        }

        return new(theoryData);
    }

    public override bool SupportsDiscoveryEnumeration() => false;
}
