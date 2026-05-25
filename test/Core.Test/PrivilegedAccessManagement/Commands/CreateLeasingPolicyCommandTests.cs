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
public class CreateLeasingPolicyCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_PersistsWithTimestampsAndValidates(LeasingPolicy policy)
    {
        var sutProvider = SetupSutProvider();
        policy.Name = "VPN + business hours";
        policy.Policy = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(policy.Policy)
            .Returns(LeasingPolicyValidationResult.Valid);
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns(new List<LeasingPolicy>());
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .CreateAsync(policy)
            .Returns(policy);

        var result = await sutProvider.Sut.CreateAsync(policy);

        Assert.Equal(_now, result.CreationDate);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<ILeasingPolicyRepository>().Received(1).CreateAsync(policy);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_EmptyName_ThrowsBadRequest(LeasingPolicy policy)
    {
        var sutProvider = SetupSutProvider();
        policy.Name = "  ";

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(policy));
        Assert.Contains("Name is required", ex.Message);
        await sutProvider.GetDependency<ILeasingPolicyRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InvalidPolicy_ThrowsBadRequest(LeasingPolicy policy)
    {
        var sutProvider = SetupSutProvider();
        policy.Name = "test";
        policy.Policy = """{"kind":"bogus"}""";
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(policy.Policy)
            .Returns(LeasingPolicyValidationResult.Invalid("Unsupported policy kind"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(policy));
        Assert.Equal("Unsupported policy kind", ex.Message);
        await sutProvider.GetDependency<ILeasingPolicyRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DuplicateName_ThrowsBadRequest(LeasingPolicy policy, LeasingPolicy existing)
    {
        var sutProvider = SetupSutProvider();
        policy.Name = "duplicate";
        policy.Policy = """{"kind":"human_approval"}""";
        existing.OrganizationId = policy.OrganizationId;
        existing.Name = "Duplicate";   // case-insensitive collision
        sutProvider.GetDependency<ILeasingPolicyValidator>()
            .Validate(policy.Policy)
            .Returns(LeasingPolicyValidationResult.Valid);
        sutProvider.GetDependency<ILeasingPolicyRepository>()
            .GetManyByOrganizationIdAsync(policy.OrganizationId)
            .Returns(new List<LeasingPolicy> { existing });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(policy));
        Assert.Contains("already exists", ex.Message);
        await sutProvider.GetDependency<ILeasingPolicyRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    private static SutProvider<CreateLeasingPolicyCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<CreateLeasingPolicyCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
