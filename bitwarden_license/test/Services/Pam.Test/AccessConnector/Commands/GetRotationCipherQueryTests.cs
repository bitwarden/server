using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

public class GetRotationCipherQueryTests
{
    private static readonly DateTime _now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _claimedAt = new(2026, 10, 5, 11, 55, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetAsync_WrongAccessConnector_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt)
    {
        var sutProvider = Setup();
        attempt.Action = PamRotationAttemptAction.None;
        // attempt.ClaimedByAccessConnectorId is a different AutoFixture guid than accessConnectorId.
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_AttemptAlreadyReported_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        var sutProvider = Setup();
        ClaimedBy(accessConnectorId, attempt, job);
        attempt.Action = PamRotationAttemptAction.Errored;
        Returns(sutProvider, attempt, job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_JobNotClaimed_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        var sutProvider = Setup();
        ClaimedBy(accessConnectorId, attempt, job);
        job.Action = PamRotationJobAction.None;
        Returns(sutProvider, attempt, job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_JobClaimedByDifferentAccessConnector_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, Guid otherAccessConnectorId)
    {
        var sutProvider = Setup();
        ClaimedBy(accessConnectorId, attempt, job);
        job.ClaimedByAccessConnectorId = otherAccessConnectorId;
        Returns(sutProvider, attempt, job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ClaimPastTheJobDeadline_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        // The timeout sweep has not run, but the job already derives as TimedOut.
        var sutProvider = Setup();
        ClaimedBy(accessConnectorId, attempt, job);
        job.ExpiresAt = _now;
        Returns(sutProvider, attempt, job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_AttemptFromAnEarlierClaim_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        // Released and re-claimed by the same access connector: the job is Claimed again, but by a newer attempt.
        var sutProvider = Setup();
        ClaimedBy(accessConnectorId, attempt, job);
        job.ClaimedAt = _claimedAt.AddMinutes(1);
        Returns(sutProvider, attempt, job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HappyPath_ReturnsCipherOfConfigsCipherId(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config, Cipher cipher)
    {
        var sutProvider = Setup();
        ClaimedBy(accessConnectorId, attempt, job);
        job.RotationConfigId = config.Id;
        cipher.Id = config.CipherId;
        Returns(sutProvider, attempt, job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(config.CipherId).Returns(cipher);

        var result = await sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id);

        Assert.Same(cipher, result);
    }

    private static SutProvider<GetRotationCipherQuery> Setup()
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void ClaimedBy(Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        attempt.JobId = job.Id;
        attempt.ClaimedByAccessConnectorId = accessConnectorId;
        attempt.Action = PamRotationAttemptAction.None;
        attempt.CreationDate = _claimedAt;
        job.Action = PamRotationJobAction.Claimed;
        job.ClaimedByAccessConnectorId = accessConnectorId;
        job.ClaimedAt = _claimedAt;
        job.ExpiresAt = _now.AddHours(1);
    }

    private static void Returns(
        SutProvider<GetRotationCipherQuery> sutProvider, PamRotationAttempt attempt, PamRotationJob job)
    {
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
    }
}
