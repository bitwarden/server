using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Api.Utilities.DiagnosticTools;
using Bit.Core;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Utilities.DiagnosticTools;

public class EventDiagnosticLoggerTests
{
    [Theory, BitAutoData]
    public void LogAggregateData_PublicApi_FeatureFlagEnabled_LogsInformation(
        Guid organizationId,
        EventFilterRequestModel request)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var ev1 = Substitute.For<IEvent>();
        ev1.Date.Returns(DateTime.UtcNow.AddDays(-2));
        var ev2 = Substitute.For<IEvent>();
        ev2.Date.Returns(DateTime.UtcNow.AddDays(-1));

        var eventResponses = new List<EventResponseModel>
        {
            new EventResponseModel(ev1),
            new EventResponseModel(ev2)
        };
        var response = new PagedListResponseModel<EventResponseModel>(eventResponses, "continuation-token");

        // Act
        logger.LogAggregateData(featureService, organizationId, response, request);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString())),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_PublicApi_FeatureFlagDisabled_DoesNotLog(
        Guid organizationId,
        EventFilterRequestModel request)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(false);

        var ev = Substitute.For<IEvent>();
        ev.Date.Returns(DateTime.UtcNow);

        var eventResponses = new List<EventResponseModel>
        {
            new EventResponseModel(ev)
        };
        var response = new PagedListResponseModel<EventResponseModel>(eventResponses, "token");

        // Act
        logger.LogAggregateData(featureService, organizationId, response, request);

        // Assert
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_AdminConsoleApi_FeatureFlagEnabled_LogsInformation(
        Guid organizationId,
        DateTime start,
        DateTime end)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var ev1 = Substitute.For<IEvent>();
        ev1.Date.Returns(DateTime.UtcNow.AddDays(-3));
        var ev2 = Substitute.For<IEvent>();
        ev2.Date.Returns(DateTime.UtcNow.AddDays(-1));

        var eventResponses = new List<Bit.Api.Models.Response.EventResponseModel>
        {
            new Bit.Api.Models.Response.EventResponseModel(ev1),
            new Bit.Api.Models.Response.EventResponseModel(ev2)
        };
        var continuationToken = "test-token";

        // Act
        logger.LogAggregateData(featureService, organizationId, continuationToken, eventResponses, start, end);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains(organizationId.ToString())),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_AdminConsoleApi_FeatureFlagDisabled_DoesNotLog(
        Guid organizationId,
        DateTime start,
        DateTime end)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(false);

        var ev = Substitute.For<IEvent>();
        ev.Date.Returns(DateTime.UtcNow);

        var eventResponses = new List<Bit.Api.Models.Response.EventResponseModel>
        {
            new Bit.Api.Models.Response.EventResponseModel(ev)
        };

        // Act
        logger.LogAggregateData(featureService, organizationId, "token", eventResponses, start, end);

        // Assert
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_PublicApi_EmptyData_LogsZeroCount(
        Guid organizationId,
        EventFilterRequestModel request)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var response = new PagedListResponseModel<EventResponseModel>(new List<EventResponseModel>(), null);

        // Act
        logger.LogAggregateData(featureService, organizationId, response, request);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_AdminConsoleApi_EmptyData_LogsZeroCount(
        Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var eventResponses = new List<Bit.Api.Models.Response.EventResponseModel>();

        // Act
        logger.LogAggregateData(featureService, organizationId, null, eventResponses, null, null);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_PublicApi_HasContinuationToken_LogsHasMoreTrue(
        Guid organizationId,
        EventFilterRequestModel request)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var ev = Substitute.For<IEvent>();
        ev.Date.Returns(DateTime.UtcNow);

        var eventResponses = new List<EventResponseModel>
        {
            new EventResponseModel(ev)
        };
        var response = new PagedListResponseModel<EventResponseModel>(eventResponses, "has-more-token");

        // Act
        logger.LogAggregateData(featureService, organizationId, response, request);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains("HasMore")),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_AdminConsoleApi_NoContinuationToken_LogsHasMoreFalse(
        Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var ev = Substitute.For<IEvent>();
        ev.Date.Returns(DateTime.UtcNow);

        var eventResponses = new List<Bit.Api.Models.Response.EventResponseModel>
        {
            new Bit.Api.Models.Response.EventResponseModel(ev)
        };

        // Act
        logger.LogAggregateData(featureService, organizationId, null, eventResponses, null, null);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }
}
