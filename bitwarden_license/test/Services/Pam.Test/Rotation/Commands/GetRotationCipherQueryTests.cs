using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Commands;

[SutProviderCustomize]
public class GetRotationCipherQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_WrongDaemon_ThrowsNotFound(
        Guid daemonId, PamRotationAttempt attempt)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.Status = PamRotationAttemptStatus.Executing;
        // attempt.ClaimedByDaemonId is a different AutoFixture guid than daemonId.
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(daemonId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_AttemptNotExecuting_ThrowsNotFound(Guid daemonId, PamRotationAttempt attempt)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByDaemonId = daemonId;
        attempt.Status = PamRotationAttemptStatus.Errored;
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(daemonId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_JobNotClaimed_ThrowsNotFound(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByDaemonId = daemonId;
        attempt.Status = PamRotationAttemptStatus.Executing;
        attempt.JobId = job.Id;
        job.Status = PamRotationJobStatus.Pending;
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(daemonId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_JobClaimedByDifferentDaemon_ThrowsNotFound(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByDaemonId = daemonId;
        attempt.Status = PamRotationAttemptStatus.Executing;
        attempt.JobId = job.Id;
        job.Status = PamRotationJobStatus.Claimed;
        // job.ClaimedByDaemonId is a different AutoFixture guid than daemonId.
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(daemonId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HappyPath_ReturnsCipherOfConfigsCipherId(
        Guid daemonId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config, Cipher cipher)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByDaemonId = daemonId;
        attempt.Status = PamRotationAttemptStatus.Executing;
        attempt.JobId = job.Id;
        job.Status = PamRotationJobStatus.Claimed;
        job.ClaimedByDaemonId = daemonId;
        job.RotationConfigId = config.Id;
        cipher.Id = config.CipherId;
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(config.CipherId).Returns(cipher);

        var result = await sutProvider.Sut.GetAsync(daemonId, attempt.Id);

        Assert.Same(cipher, result);
    }
}
