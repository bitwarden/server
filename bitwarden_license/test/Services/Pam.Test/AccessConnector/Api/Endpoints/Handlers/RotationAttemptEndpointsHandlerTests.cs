using Bit.Core.Context;
using Bit.Pam.Enums;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Endpoints.Handlers;

[SutProviderCustomize]
public class RotationAttemptEndpointsHandlerTests
{
    [Theory, BitAutoData]
    public async Task Success_ReportsForTheCallingAccessConnector(
        Guid accessConnectorId, Guid attemptId, SutProvider<RotationAttemptEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().PamAccessConnectorId.Returns(accessConnectorId);
        var model = new ReportRotationSucceededRequestModel
        {
            SessionTermination = PamSessionTerminationOutcome.Terminated,
        };

        await sutProvider.Sut.Success(attemptId, model);

        await sutProvider.GetDependency<IReportRotationSucceededCommand>().Received(1)
            .ReportSucceededAsync(accessConnectorId, attemptId, PamSessionTerminationOutcome.Terminated);
    }

    [Theory, BitAutoData]
    public async Task Failure_ReportsTheCodeAndDetailForTheCallingAccessConnector(
        Guid accessConnectorId, Guid attemptId, SutProvider<RotationAttemptEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().PamAccessConnectorId.Returns(accessConnectorId);
        var model = new ReportRotationFailedRequestModel
        {
            ErrorCode = "target_unreachable",
            Detail = "timed out",
            SyncState = PamRotationSyncState.TargetUnchanged,
        };

        await sutProvider.Sut.Failure(attemptId, model);

        await sutProvider.GetDependency<IReportRotationFailedCommand>().Received(1).ReportFailedAsync(
            accessConnectorId, attemptId, "target_unreachable: timed out", PamRotationSyncState.TargetUnchanged);
    }
}
