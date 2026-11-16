using Bit.Core.Context;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Endpoints.Handlers;

[SutProviderCustomize]
public class RotationJobEndpointsHandlerTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetJobs_ListsTheCallingAccessConnectorsClaimableJobs(
        Guid accessConnectorId, PamClaimableJob first, PamClaimableJob second)
    {
        var sutProvider = Setup(accessConnectorId);
        sutProvider.GetDependency<IPamRotationJobRepository>()
            .GetManyClaimableByAccessConnectorIdAsync(accessConnectorId, _now)
            .Returns(new List<PamClaimableJob> { first, second });

        var response = await sutProvider.Sut.GetJobs();

        Assert.Equal([first.Id, second.Id], response.Data.Select(job => job.JobId));
        Assert.Equal([first.TargetSystemId, second.TargetSystemId], response.Data.Select(job => job.TargetSystemId));
    }

    [Theory, BitAutoData]
    public async Task Claim_ClaimsForTheCallingAccessConnector(
        Guid accessConnectorId, Guid jobId, Guid attemptId, Guid targetSystemId, Guid cipherId)
    {
        var sutProvider = Setup(accessConnectorId);
        sutProvider.GetDependency<IClaimRotationJobCommand>().ClaimAsync(accessConnectorId, jobId).Returns(
            new PamRotationClaimResult
            {
                Outcome = PamRotationClaimOutcome.Claimed,
                AttemptId = attemptId,
                JobId = jobId,
                Source = PamRotationSource.Scheduled,
                TargetSystemId = targetSystemId,
                TargetSystemName = "Entra",
                TargetSystemSealedData = "target-sealed",
                TargetSystemWrappedKey = "target-key",
                CipherId = cipherId,
                RotationConfigSealedData = "config-sealed",
                RotationConfigWrappedKey = "config-key",
                ExecuteBy = _now.AddMinutes(15),
            });

        var response = await sutProvider.Sut.Claim(jobId);

        Assert.Equal(attemptId, response.AttemptId);
        Assert.Equal(jobId, response.JobId);
        Assert.Equal(targetSystemId, response.TargetSystemId);
        Assert.Equal(cipherId, response.CipherId);
        Assert.Equal("target-sealed", response.TargetSystemSealedData);
        Assert.Equal("config-key", response.RotationConfigWrappedKey);
    }

    private static SutProvider<RotationJobEndpointsHandler> Setup(Guid accessConnectorId)
    {
        var sutProvider = new SutProvider<RotationJobEndpointsHandler>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<ICurrentContext>().PamAccessConnectorId.Returns(accessConnectorId);
        return sutProvider;
    }
}
