using Bit.Core.AdminConsole.Utilities.DebuggingInstruments;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities.DebuggingInstruments;

public class UserInviteDebuggingLoggerTests
{
    [Fact]
    public void LogUserInviteStateDiagnostics_WhenInvitedUserHasNoEmail_LogsWarning()
    {
        // Arrange
        var organizationUser = new OrganizationUser
        {
            OrganizationId = Guid.Parse("3e1f2196-9ad6-4ba7-b69d-ba33bc25f774"),
            Status = OrganizationUserStatusType.Invited,
            Email = string.Empty,
            UserId = Guid.Parse("93fbddd1-e96d-491d-a38b-6966ff59ac28"),
            Id = Guid.Parse("326f043f-afdc-47e5-9646-a76ab709b69a"),
        };

        var logger = Substitute.For<ILogger>();

        // Act
        logger.LogUserInviteStateDiagnostics(organizationUser);

        // Assert
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(errorMessage =>
                errorMessage.ToString().Contains("Warning invalid invited state")
                && errorMessage.ToString().Contains(organizationUser.OrganizationId.ToString())
                && errorMessage.ToString().Contains(organizationUser.UserId.ToString())
                && errorMessage.ToString().Contains(organizationUser.Id.ToString())
            ),
            null,
            Arg.Any<Func<object, Exception, string>>()
        );
    }

    public static IEnumerable<object[]> ConfirmedOrAcceptedTestCases =>
    [
       new object[] { OrganizationUserStatusType.Accepted },
        new object[] { OrganizationUserStatusType.Confirmed },
    ];

    [Theory]
    [MemberData(nameof(ConfirmedOrAcceptedTestCases))]
    public void LogUserInviteStateDiagnostics_WhenNonInvitedUserHasEmail_LogsWarning(OrganizationUserStatusType userStatusType)
    {
        // Arrange
        var organizationUser = new OrganizationUser
        {
            OrganizationId = Guid.Parse("3e1f2196-9ad6-4ba7-b69d-ba33bc25f774"),
            Status = userStatusType,
            Email = "someone@example.com",
            UserId = Guid.Parse("93fbddd1-e96d-491d-a38b-6966ff59ac28"),
            Id = Guid.Parse("326f043f-afdc-47e5-9646-a76ab709b69a"),
        };

        var logger = Substitute.For<ILogger>();

        // Act
        logger.LogUserInviteStateDiagnostics(organizationUser);

        // Assert
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(errorMessage =>
                errorMessage.ToString().Contains("Warning invalid confirmed or accepted state")
                && errorMessage.ToString().Contains(organizationUser.OrganizationId.ToString())
                && errorMessage.ToString().Contains(organizationUser.UserId.ToString())
                && errorMessage.ToString().Contains(organizationUser.Id.ToString())
                // Ensure that no PII is included in the log.
                && !errorMessage.ToString().Contains(organizationUser.Email)
            ),
            null,
            Arg.Any<Func<object, Exception, string>>()
        );
    }


    public static List<object[]> ShouldNotLogTestCases =>
    [
        new object[] { new OrganizationUser { Status = OrganizationUserStatusType.Accepted, Email = null } },
        new object[] { new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Email = null } },
        new object[] { new OrganizationUser { Status = OrganizationUserStatusType.Invited, Email = "someone@example.com" } },
        new object[] { new OrganizationUser { Status = OrganizationUserStatusType.Revoked, Email = null } },
    ];

    [Theory]
    [MemberData(nameof(ShouldNotLogTestCases))]
    public void LogUserInviteStateDiagnostics_WhenStateAreValid_ShouldNotLog(OrganizationUser user)
    {
        var logger = Substitute.For<ILogger>();

        // Act
        logger.LogUserInviteStateDiagnostics(user);

        // Assert
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }
}
