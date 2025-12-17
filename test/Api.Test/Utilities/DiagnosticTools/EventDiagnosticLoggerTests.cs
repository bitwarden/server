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
        oldestEvent.Date.Returns(DateTime.UtcNow.AddDays(-3));

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
                o.ToString().Contains($"newest record:{newestEvent.Date:O}") &&
                o.ToString().Contains($"oldest record:{oldestEvent.Date:O}") &&
                o.ToString().Contains("HasMore:True") &&
                o.ToString().Contains($"Start:{request.Start:o}") &&
                o.ToString().Contains($"End:{request.End:o}") &&
                o.ToString().Contains($"ActingUserId:{request.ActingUserId}") &&
                o.ToString().Contains($"ItemId:{request.ItemId}"))
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
    public void LogAggregateData_WithInternalResponse_FeatureFlagDisabled_DoesNotLog(Guid organizationId)
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
    public void LogAggregateData_WithInternalResponse_EmptyData_LogsZeroCount(
        Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        Bit.Api.Models.Response.EventResponseModel[] emptyEvents = [];

        // Act
        logger.LogAggregateData(featureService, organizationId, emptyEvents, null, null, null);

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
    public void LogAggregateData_WithInternalResponse_FeatureFlagEnabled_LogsInformation(
        Guid organizationId)
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.EventDiagnosticLogging).Returns(true);

        var newestEvent = Substitute.For<IEvent>();
        newestEvent.Date.Returns(DateTime.UtcNow);
        var middleEvent = Substitute.For<IEvent>();
        middleEvent.Date.Returns(DateTime.UtcNow.AddDays(-1));
        var oldestEvent = Substitute.For<IEvent>();
        oldestEvent.Date.Returns(DateTime.UtcNow.AddDays(-2));

        var events = new List<Bit.Api.Models.Response.EventResponseModel>
        {
            new (newestEvent),
            new (middleEvent),
            new (oldestEvent)
        };

        var queryStart = DateTime.UtcNow.AddMinutes(-3);
        var queryEnd = DateTime.UtcNow;
        const string continuationToken = "continuation-token";

        // Act
        logger.LogAggregateData(featureService, organizationId, events, continuationToken, queryStart, queryEnd);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString().Contains(organizationId.ToString()) &&
                o.ToString().Contains($"Event count:{events.Count}") &&
                o.ToString().Contains($"newest record:{newestEvent.Date:O}") &&
                o.ToString().Contains($"oldest record:{oldestEvent.Date:O}") &&
                o.ToString().Contains("HasMore:True") &&
                o.ToString().Contains($"Start:{queryStart:o}") &&
                o.ToString().Contains($"End:{queryEnd:o}"))
            ,
            null,
            Arg.Any<Func<object, Exception, string>>());
    }
}
