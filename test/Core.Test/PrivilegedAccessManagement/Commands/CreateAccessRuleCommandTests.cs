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
public class CreateAccessRuleCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_PersistsWithTimestampsAndValidates(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Rule = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Rule)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule);

        Assert.Equal(_now, result.CreationDate);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1).CreateAsync(rule);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_EmptyName_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "  ";

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule));
        Assert.Contains("Name is required", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InvalidRule_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "test";
        rule.Rule = """{"kind":"bogus"}""";
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Rule)
            .Returns(AccessRuleValidationResult.Invalid("Unsupported rule kind"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule));
        Assert.Equal("Unsupported rule kind", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DuplicateName_ThrowsBadRequest(AccessRule rule, AccessRule existing)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "duplicate";
        rule.Rule = """{"kind":"human_approval"}""";
        existing.OrganizationId = rule.OrganizationId;
        existing.Name = "Duplicate";   // case-insensitive collision
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Rule)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule> { existing });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule));
        Assert.Contains("already exists", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    private static SutProvider<CreateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<CreateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
