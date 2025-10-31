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
    public void LogAggregateData_WithPublicResponse_FeatureFlagEnabled_LogsInformation(
        Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var request = new EventFilterRequestModel()
        {
            Start = DateTime.UtcNow.AddMinutes(-3),
            End = DateTime.UtcNow,
            ActingUserId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
        };

        var newestEvent = Substitute.For<IEvent>();
        newestEvent.Date.Returns(DateTime.UtcNow);
        var middleEvent = Substitute.For<IEvent>();
        middleEvent.Date.Returns(DateTime.UtcNow.AddDays(-1));
        var oldestEvent = Substitute.For<IEvent>();
        oldestEvent.Date.Returns(DateTime.UtcNow.AddDays(-2));

        var eventResponses = new List<EventResponseModel>
        {
            new (newestEvent),
            new (middleEvent),
            new (oldestEvent)
        };
        var response = new PagedListResponseModel<EventResponseModel>(eventResponses, "continuation-token");

        // Act
        logger.LogAggregateData(featureService, organizationId, response, request);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains(organizationId.ToString()) &&
                o.ToString().Contains($"Event count:{eventResponses.Count}") &&
                o.ToString().Contains("Request Filters Start:") &&
                o.ToString().Contains("End:") &&
                o.ToString().Contains($"ActingUserId:{request.ActingUserId}") &&
                o.ToString().Contains($"ItemId:{request.ItemId}") &&
                o.ToString().Contains($"newest record:{newestEvent.Date:O}") &&
                o.ToString().Contains($"oldest record:{oldestEvent.Date:O}") &&
                o.ToString().Contains("HasMore:True"))
            ,
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_WithPublicResponse_FeatureFlagDisabled_DoesNotLog(
        Guid organizationId,
        EventFilterRequestModel request)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(false);

        PagedListResponseModel<EventResponseModel> dummy = null;

        // Act
        logger.LogAggregateData(featureService, organizationId, dummy, request);

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

        var oldestDate = DateTime.UtcNow.AddDays(-3);
        var newestDate = DateTime.UtcNow.AddDays(-1);

        var ev1 = Substitute.For<IEvent>();
        ev1.Date.Returns(oldestDate);
        var ev2 = Substitute.For<IEvent>();
        ev2.Date.Returns(newestDate);

        var eventResponses = new List<Bit.Api.Models.Response.EventResponseModel>
        {
            new (ev1),
            new (ev2)
        };
        var continuationToken = "test-token";

        // Act
        logger.LogAggregateData(featureService, organizationId, continuationToken, eventResponses, start, end);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains(organizationId.ToString()) &&
                o.ToString().Contains("Returned 2 events") &&
                o.ToString().Contains("HasMore: True") &&
                o.ToString().Contains("oldest record") &&
                o.ToString().Contains("newest record")),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_AdminConsoleApi_FeatureFlagDisabled_DoesNotLog(Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(false);


        // Act
        logger.LogAggregateData(featureService, organizationId, null, null, null, null);

        // Assert
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public void LogAggregateData_WithPublicResponse_EmptyData_LogsZeroCount(
        Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var request = new EventFilterRequestModel()
        {
            Start = null,
            End = null,
            ActingUserId = null,
            ItemId = null,
            ContinuationToken = null,
        };
        var response = new PagedListResponseModel<EventResponseModel>(new List<EventResponseModel>(), null);

        // Act
        logger.LogAggregateData(featureService, organizationId, response, request);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains(organizationId.ToString()) &&
                o.ToString().Contains("Event count:0") &&
                o.ToString().Contains("HasMore:False")),
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
            Arg.Is<object>(o =>
                o.ToString().Contains(organizationId.ToString()) &&
                o.ToString().Contains("Returned 0 events") &&
                o.ToString().Contains("HasMore: False")),
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
            new (ev)
        };

        // Act
        logger.LogAggregateData(featureService, organizationId, null, eventResponses, null, null);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains(organizationId.ToString()) &&
                o.ToString().Contains("Returned 1 events") &&
                o.ToString().Contains("HasMore: False")),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }
}
