using System.Diagnostics;
using System.Reflection;
using AutoFixture;
using Bit.Test.Common.Helpers;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class BitMemberAutoDataAttribute : MemberDataAttributeBase
{
    private readonly Func<IFixture> _createFixture;

    public BitMemberAutoDataAttribute(string memberName, params object[] parameters) :
        this(() => new Fixture(), memberName, parameters)
    { }

    public BitMemberAutoDataAttribute(Func<IFixture> createFixture, string memberName, params object[] parameters) :
        base(memberName, parameters)
    {
        _createFixture = createFixture;
    }

    private MethodInfo? _testMethod;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        _testMethod = testMethod;
        return base.GetData(testMethod, disposalTracker);
    }

    protected override ITheoryDataRow ConvertDataRow(object dataRow)
    {
        // Unwrap a possible ITheoryDataRow
        object?[] fixedItems;
        if (dataRow is ITheoryDataRow theoryDataRow)
        {
            fixedItems = theoryDataRow.GetData();
        }
        else
        {
            fixedItems = (dataRow as object?[])!;
        }
        Debug.Assert(_testMethod is not null, "GetData expected to be called first.");
        return new TheoryDataRow(BitAutoDataAttributeHelpers.GetData(_testMethod, _createFixture(), fixedItems).First());
    }
}
