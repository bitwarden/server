
using Bit.Core.AdminConsole.Utilities.DebuggingInstruments;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class UserInviteDebuggingLoggerTests
{
    [Theory, BitAutoData]
    public void Log_WhenInvitedUserHasNoEmail_LogsWarning([OrganizationUser(status: OrganizationUserStatusType.Invited)] OrganizationUser user)
    {
        // Arrange
        user.Email = string.Empty;

        var logger = Substitute.For<ILogger<UserInviteDebuggingLogger>>();
        var sut = new UserInviteDebuggingLogger(logger);

        // Act
        sut.Log(user);

        // Assert
        logger.Received(1).LogWarning("Warning invalid invited state.");
    }

    [Theory, BitAutoData]
    public void Log_WhenNonInvitedUserHasEmail_LogsWarning([OrganizationUser(status: OrganizationUserStatusType.Confirmed)] OrganizationUser user)
    {
        // Arrange
        user.Email = "someone@example.com";

        var logger = Substitute.For<ILogger<UserInviteDebuggingLogger>>();
        var sut = new UserInviteDebuggingLogger(logger);

        // Act
        sut.Log(user);

        // Assert
        logger.Received(1).LogWarning("Warning invalid non invited state.");
    }


    public static List<object[]> TestCases =>
    [
        new object[] { new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Email = null } },
        new object[] { new OrganizationUser { Status = OrganizationUserStatusType.Invited, Email = "someone@example.com" } },
    ];

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Log_WhenStateAreValid_ShouldNotLog(OrganizationUser user)
    {
        // Arrange
        var logger = Substitute.For<ILogger<UserInviteDebuggingLogger>>();
        var sut = new UserInviteDebuggingLogger(logger);

        // Act
        sut.Log(user);

        // Assert
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());

    }
}
