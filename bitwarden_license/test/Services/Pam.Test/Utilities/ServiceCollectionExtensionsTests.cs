using Bit.Core.Pam.Services;
using Bit.Services.Pam.Services;
using Bit.Services.Pam.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Utilities;

/// <summary>
/// A missing PAM registration is invisible at compile time and surfaces as a 500 on the first request that needs it.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    // An empty configuration root leaves PamRotationOptions at its default, which is all these need.
    private static IServiceCollection PamServices() =>
        new ServiceCollection().AddPamServices(new ConfigurationBuilder().Build());

    [Fact]
    public void AddPamServices_RegistersEveryPamOwnedDependency()
    {
        var services = PamServices();
        var registered = services.Select(d => d.ServiceType).ToHashSet();
        var pamAssembly = typeof(ServiceCollectionExtensions).Assembly;

        var implementations = services
            .Select(d => d.ImplementationType)
            .Where(t => t is not null && t.Assembly == pamAssembly)
            .Distinct()
            .ToList();

        Assert.NotEmpty(implementations);

        var missing = new List<string>();
        foreach (var implementation in implementations)
        {
            foreach (var constructor in implementation!.GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    // Only PAM's own seams are this method's responsibility; the host registers the rest, such as
                    // repositories and ICurrentContext.
                    if (parameter.ParameterType.Assembly != pamAssembly || !parameter.ParameterType.IsInterface)
                    {
                        continue;
                    }

                    if (!registered.Contains(parameter.ParameterType))
                    {
                        missing.Add($"{implementation.Name} needs {parameter.ParameterType.Name}");
                    }
                }
            }
        }

        Assert.Empty(missing);
    }

    /// <summary>
    /// AddBaseServices already registers the open-source default; a TryAdd would leave every PAM-governed cipher
    /// readable.
    /// </summary>
    [Fact]
    public void AddPamServices_OverridesTheDefaultCipherLeaseGate()
    {
        var services = PamServices();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICipherLeaseGate));
        Assert.Equal(typeof(CipherLeaseGate), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddPamServices_RegistersTimeProvider()
    {
        // Every command reads the clock from TimeProvider.
        var services = PamServices();

        Assert.Contains(services, d => d.ServiceType == typeof(TimeProvider));
    }

    [Theory]
    [InlineData(typeof(IAccessAuditEventEmitter), typeof(AccessAuditEventEmitter))]
    [InlineData(typeof(IApproverInboxNotifier), typeof(ApproverInboxNotifier))]
    [InlineData(typeof(IRequesterNotifier), typeof(RequesterNotifier))]
    [InlineData(typeof(IAccessMailNotifier), typeof(AccessMailNotifier))]
    public void AddPamServices_RegistersSideChannelSeam(Type serviceType, Type expectedImplementation)
    {
        var services = PamServices();

        var descriptor = Assert.Single(services, d => d.ServiceType == serviceType);
        Assert.Equal(expectedImplementation, descriptor.ImplementationType);
    }

    /// <remarks>Discovered by reflection so a new handler cannot be left out.</remarks>
    public static TheoryData<Type> EndpointHandlers()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(ServiceCollectionExtensions).Assembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                                 && t.Name.EndsWith("EndpointsHandler", StringComparison.Ordinal))
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            data.Add(type);
        }

        return data;
    }

    [Theory, MemberData(nameof(EndpointHandlers))]
    public void AddPamServices_RegistersEndpointHandler(Type handlerType)
    {
        // The Minimal API endpoints resolve their handler from DI.
        var services = PamServices();

        Assert.Contains(services, d => d.ServiceType == handlerType);
    }

    [Fact]
    public void EndpointHandlers_DiscoversEveryHandlerInTheAssembly()
    {
        // Guards against the theory above silently matching nothing.
        Assert.True(EndpointHandlers().Count >= 10);
    }
}
