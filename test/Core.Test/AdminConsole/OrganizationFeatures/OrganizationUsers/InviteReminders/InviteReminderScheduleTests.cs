using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public class InviteReminderScheduleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(120)]
    public void GetIntervalHours_WhenZeroOrMore_ReturnsConfiguredValue(int intervalHours)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.OrganizationInviteReminderIntervalHours.Returns(intervalHours);

        var result = InviteReminderSchedule.GetIntervalHours(globalSettings, Substitute.For<ILogger>());

        Assert.Equal(intervalHours, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-120)]
    public void GetIntervalHours_WhenBelowZero_ReturnsDefault(int intervalHours)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.OrganizationInviteReminderIntervalHours.Returns(intervalHours);

        var result = InviteReminderSchedule.GetIntervalHours(globalSettings, Substitute.For<ILogger>());

        Assert.Equal(GlobalSettings.DefaultOrganizationInviteReminderIntervalHours, result);
    }
}
