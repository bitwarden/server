using Bit.Core.Exceptions;
using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.OrganizationFeatures.Commands;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Core.PrivilegedAccessManagement.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Commands;

[SutProviderCustomize]
public class UpdateAccessRuleCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_UpdatesFieldsAndBumpsRevision(AccessRule existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        update.Description = "new description";
        update.Rule = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Rule)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<AccessRule> { existing });

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update);

        Assert.Equal("renamed", result.Name);
        Assert.Equal("new description", result.Description);
        Assert.Equal(update.Rule, result.Rule);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1).ReplaceAsync(existing);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_MissingExisting_ThrowsNotFound(AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((AccessRule?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), update));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(AccessRule existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var differentOrg = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(differentOrg, existing.Id, update));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidRule_ThrowsBadRequest(AccessRule existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "ok";
        update.Rule = """{"kind":"bogus"}""";
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Rule)
            .Returns(AccessRuleValidationResult.Invalid("nope"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(orgId, existing.Id, update));
        Assert.Equal("nope", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    private static SutProvider<UpdateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<UpdateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
