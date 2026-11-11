using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.SecretsManager.Entities;
using Xunit;
using Event = Bit.Core.Entities.Event;
using IEventRepository = Bit.Core.Repositories.IEventRepository;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

/// <summary>
/// Covers <c>GetManyBySecretAsync</c> and <c>GetManyByProjectAsync</c>. When the secret or project has
/// been deleted, callers pass a stand-in entity whose <c>OrganizationId</c> comes from the request, so
/// the repository must scope results to that organization rather than trusting the resource id alone.
/// No Secret or Project rows are created, which mirrors that deleted-resource case. <c>SelfHosted = true</c>
/// resolves to the Dapper implementation on SQL Server and the EF implementation on the other providers.
/// </summary>
public class EventRepositorySecretsManagerTests
{
    [Theory, DatabaseData(SelfHosted = true)]
    public async Task GetManyBySecretAsync_ReturnsEventsForTheSecretsOrganization(IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();
        var secretId = Guid.NewGuid();
        await sut.CreateManyAsync(BuildEvents(organizationId, EventType.Secret_Retrieved, 2, secretId: secretId));

        var result = await sut.GetManyBySecretAsync(
            new Secret { Id = secretId, OrganizationId = organizationId },
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), new PageOptions { PageSize = 100 });

        Assert.Equal(2, result.Data.Count);
        Assert.All(result.Data, e => Assert.Equal(organizationId, e.OrganizationId));
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task GetManyBySecretAsync_DoesNotReturnAnotherOrganizationsEvents(IEventRepository sut)
    {
        var victimOrganizationId = Guid.NewGuid();
        var callerOrganizationId = Guid.NewGuid();
        var secretId = Guid.NewGuid();
        await sut.CreateManyAsync(BuildEvents(victimOrganizationId, EventType.Secret_Retrieved, 2, secretId: secretId));

        var result = await sut.GetManyBySecretAsync(
            new Secret { Id = secretId, OrganizationId = callerOrganizationId },
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), new PageOptions { PageSize = 100 });

        Assert.Empty(result.Data);
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task GetManyByProjectAsync_ReturnsEventsForTheProjectsOrganization(IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await sut.CreateManyAsync(BuildEvents(organizationId, EventType.Project_Retrieved, 2, projectId: projectId));

        var result = await sut.GetManyByProjectAsync(
            new Project { Id = projectId, OrganizationId = organizationId },
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), new PageOptions { PageSize = 100 });

        Assert.Equal(2, result.Data.Count);
        Assert.All(result.Data, e => Assert.Equal(organizationId, e.OrganizationId));
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task GetManyByProjectAsync_DoesNotReturnAnotherOrganizationsEvents(IEventRepository sut)
    {
        var victimOrganizationId = Guid.NewGuid();
        var callerOrganizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await sut.CreateManyAsync(BuildEvents(victimOrganizationId, EventType.Project_Retrieved, 2, projectId: projectId));

        var result = await sut.GetManyByProjectAsync(
            new Project { Id = projectId, OrganizationId = callerOrganizationId },
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), new PageOptions { PageSize = 100 });

        Assert.Empty(result.Data);
    }

    private static List<Event> BuildEvents(Guid organizationId, EventType type, int count,
        Guid? secretId = null, Guid? projectId = null) =>
        Enumerable.Range(0, count)
            .Select(i => new Event
            {
                Type = type,
                OrganizationId = organizationId,
                SecretId = secretId,
                ProjectId = projectId,
                Date = DateTime.UtcNow.AddMinutes(-i),
            })
            .ToList();
}
