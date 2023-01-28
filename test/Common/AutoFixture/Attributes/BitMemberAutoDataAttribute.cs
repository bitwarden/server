using System.Reflection;
using AutoFixture;
using Bit.Test.Common.Helpers;
using Xunit;

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

    protected override object[] ConvertDataItem(MethodInfo testMethod, object item) =>
        BitAutoDataAttributeHelpers.GetData(testMethod, _createFixture(), item as object[]).First();
}
