using System.Diagnostics;
using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Xunit;

namespace Bit.Core.Test.Platform.Push;

public class PushTypeTests
{
    [Fact]
    public void AllEnumMembersHaveUniqueValue()
    {
        // No enum member should use the same value as another named member.

        var usedNumbers = new HashSet<byte>();
        var enumMembers = Enum.GetValues<PushType>();

        foreach (var enumMember in enumMembers)
        {
            if (!usedNumbers.Add((byte)enumMember))
            {
                Assert.Fail($"Enum number value ({(byte)enumMember}) on {enumMember} is already in use.");
            }
        }
    }

    [Fact]
    public void AllEnumMembersHaveNotificationInfoAttribute()
    {
        // Every enum member should be annotated with [NotificationInfo]

        foreach (var member in typeof(PushType).GetMembers(BindingFlags.Public | BindingFlags.Static))
        {
            var notificationInfoAttribute = member.GetCustomAttribute<NotificationInfoAttribute>();
            if (notificationInfoAttribute is null)
            {
                Assert.Fail($"PushType.{member.Name} is missing a required [NotificationInfo(\"team-name\", typeof(MyType))] attribute.");
            }
        }
    }

    [Fact]
    public void AllEnumValuesAreInSequence()
    {
        // There should not be any gaps in the numbers defined for an enum, that being if someone last defined 22
        // the next number used should be 23 not 24 or any other number.

        var sortedValues = Enum.GetValues<PushType>()
            .Order()
            .ToArray();

        Debug.Assert(sortedValues.Length > 0);

        var lastValue = sortedValues[0];

        foreach (var value in sortedValues[1..])
        {
            var expectedValue = ++lastValue;

            Assert.Equal(expectedValue, value);
        }
    }
}
