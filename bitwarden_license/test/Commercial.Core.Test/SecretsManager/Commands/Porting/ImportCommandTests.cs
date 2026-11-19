using Bit.Commercial.Core.SecretsManager.Commands.Porting;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Porting;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Porting;

[SutProviderCustomize]
public class ImportCommandTests
{
    private static SMImport.InnerSecret NewSecret(params Guid[] projectIds) => new()
    {
        Id = Guid.NewGuid(),
        Key = "key",
        Value = "value",
        Note = "note",
        ProjectIds = projectIds,
    };

    [Theory, BitAutoData]
    public async Task ImportAsync_SecretReferencesUnknownProject_ThrowsBadRequest_WritesNothing(
        Guid organizationId, SutProvider<ImportCommand> sutProvider)
    {
        var import = new SMImport
        {
            Projects = new[] { new SMImport.InnerProject { Id = Guid.NewGuid(), Name = "p" } },
            Secrets = new[] { NewSecret(Guid.NewGuid()) },
        };

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ImportAsync(organizationId, import));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().ImportAsync(default!);
        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().ImportAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ImportAsync_NullProjectEntry_ThrowsBadRequest_WritesNothing(
        Guid organizationId, SutProvider<ImportCommand> sutProvider)
    {
        var import = new SMImport
        {
            Projects = new SMImport.InnerProject[] { null! },
            Secrets = Array.Empty<SMImport.InnerSecret>(),
        };

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ImportAsync(organizationId, import));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().ImportAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ImportAsync_DuplicateProjectIds_ThrowsBadRequest_WritesNothing(
        Guid organizationId, Guid projectId, SutProvider<ImportCommand> sutProvider)
    {
        var import = new SMImport
        {
            Projects = new[]
            {
                new SMImport.InnerProject { Id = projectId, Name = "a" },
                new SMImport.InnerProject { Id = projectId, Name = "b" },
            },
            Secrets = Array.Empty<SMImport.InnerSecret>(),
        };

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ImportAsync(organizationId, import));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().ImportAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ImportAsync_Valid_ImportsProjectsAndSecretsWithNewIds(
        Guid organizationId, Guid projectId, SutProvider<ImportCommand> sutProvider)
    {
        var import = new SMImport
        {
            Projects = new[] { new SMImport.InnerProject { Id = projectId, Name = "p" } },
            Secrets = new[] { NewSecret(projectId), NewSecret() },
        };
        sutProvider.GetDependency<IProjectRepository>().ImportAsync(default!).ReturnsForAnyArgs(ci => ci.Arg<IEnumerable<Project>>().ToList());
        sutProvider.GetDependency<ISecretRepository>().ImportAsync(default!).ReturnsForAnyArgs(ci => ci.Arg<IEnumerable<Secret>>().ToList());

        await sutProvider.Sut.ImportAsync(organizationId, import);

        await sutProvider.GetDependency<IProjectRepository>().Received(1).ImportAsync(Arg.Is<IEnumerable<Project>>(
            ps => ps.Count() == 1 && ps.All(p => p.OrganizationId == organizationId && p.Id != projectId)));
        await sutProvider.GetDependency<ISecretRepository>().Received(1).ImportAsync(Arg.Is<IEnumerable<Secret>>(
            ss => ss.Count() == 2 && ss.Count(s => s.Projects != null && s.Projects.Count == 1 && s.Projects.First().Id != projectId) == 1));
    }
}
