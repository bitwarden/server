using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Repositories;
using Bit.OrganizationKeys.Commands;
using Bit.OrganizationKeys.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.OrganizationKeys.Test;

public class OrganizationKeysServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOrganizationKeys_RegistersTheCommandAndHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IOrganizationRepository>());
        services.AddSingleton(Substitute.For<IOrganizationUserRepository>());

        services.AddOrganizationKeys();
        using var scope = services.BuildServiceProvider().CreateScope();

        Assert.IsType<SetOrganizationKeyIdCommand>(scope.ServiceProvider.GetService<ISetOrganizationKeyIdCommand>());
        Assert.NotNull(scope.ServiceProvider.GetService<SetOrganizationKeyIdHandler>());
    }

    [Fact]
    public void AddOrganizationKeys_RegistersTheOrganizationRequirementHandler()
    {
        var services = new ServiceCollection();

        services.AddOrganizationKeys();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationHandler)
            && descriptor.ImplementationType == typeof(OrganizationRequirementHandler));
    }
}
