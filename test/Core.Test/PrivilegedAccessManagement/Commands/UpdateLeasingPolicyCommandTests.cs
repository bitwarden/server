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
public class UpdateLeasingPolicyCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_UpdatesFieldsAndBumpsRevision(LeasingPolicy existing, LeasingPolicy update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        update.Description = "new description";
        update.Policy = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(update.Policy)
            .Returns(LeasingPolicyValidationResult.Valid);
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<LeasingPolicy> { existing });

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update);

        Assert.Equal("renamed", result.Name);
        Assert.Equal("new description", result.Description);
        Assert.Equal(update.Policy, result.Policy);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<ILeasingPolicyRepository>().Received(1).ReplaceAsync(existing);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_MissingExisting_ThrowsNotFound(LeasingPolicy update)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((LeasingPolicy?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), update));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(LeasingPolicy existing, LeasingPolicy update)
    {
        var sutProvider = SetupSutProvider();
        var differentOrg = Guid.NewGuid();
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(differentOrg, existing.Id, update));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidPolicy_ThrowsBadRequest(LeasingPolicy existing, LeasingPolicy update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "ok";
        update.Policy = """{"kind":"bogus"}""";
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(update.Policy)
            .Returns(LeasingPolicyValidationResult.Invalid("nope"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(orgId, existing.Id, update));
        Assert.Equal("nope", ex.Message);
        await sutProvider.GetDependency<ILeasingPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    private static SutProvider<UpdateLeasingPolicyCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<UpdateLeasingPolicyCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
