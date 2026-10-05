using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Commands;

[SutProviderCustomize]
public class GetRotationCipherQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_WrongAccessConnector_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.Status = PamRotationAttemptStatus.Executing;
        // attempt.ClaimedByAccessConnectorId is a different AutoFixture guid than accessConnectorId.
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_AttemptNotExecuting_ThrowsNotFound(Guid accessConnectorId, PamRotationAttempt attempt)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByAccessConnectorId = accessConnectorId;
        attempt.Status = PamRotationAttemptStatus.Errored;
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_JobNotClaimed_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByAccessConnectorId = accessConnectorId;
        attempt.Status = PamRotationAttemptStatus.Executing;
        attempt.JobId = job.Id;
        job.Status = PamRotationJobStatus.Pending;
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_JobClaimedByDifferentAccessConnector_ThrowsNotFound(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByAccessConnectorId = accessConnectorId;
        attempt.Status = PamRotationAttemptStatus.Executing;
        attempt.JobId = job.Id;
        job.Status = PamRotationJobStatus.Claimed;
        // job.ClaimedByAccessConnectorId is a different AutoFixture guid than accessConnectorId.
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HappyPath_ReturnsCipherOfConfigsCipherId(
        Guid accessConnectorId, PamRotationAttempt attempt, PamRotationJob job, PamRotationConfig config, Cipher cipher)
    {
        var sutProvider = new SutProvider<GetRotationCipherQuery>().Create();
        attempt.ClaimedByAccessConnectorId = accessConnectorId;
        attempt.Status = PamRotationAttemptStatus.Executing;
        attempt.JobId = job.Id;
        job.Status = PamRotationJobStatus.Claimed;
        job.ClaimedByAccessConnectorId = accessConnectorId;
        job.RotationConfigId = config.Id;
        cipher.Id = config.CipherId;
        sutProvider.GetDependency<IPamRotationJobRepository>().GetAttemptByIdAsync(attempt.Id).Returns(attempt);
        sutProvider.GetDependency<IPamRotationJobRepository>().GetByIdAsync(job.Id).Returns(job);
        sutProvider.GetDependency<IPamRotationConfigRepository>().GetByIdAsync(config.Id).Returns(config);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(config.CipherId).Returns(cipher);

        var result = await sutProvider.Sut.GetAsync(accessConnectorId, attempt.Id);

        Assert.Same(cipher, result);
    }
}
