using Bit.Core.Pam.Services;
using Bit.Services.Pam.Services;
using Bit.Services.Pam.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Utilities;

/// <summary>
/// Guards the PAM DI graph. A missing registration here is invisible at compile time and surfaces as a 500 on the
/// first request that touches the service, so these tests assert the wiring rather than any behaviour.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    // AddPamServices binds PamRotationOptions from configuration; an empty root leaves every option at
    // its default, which is all these wiring assertions need.
    private static IServiceCollection PamServices() =>
        new ServiceCollection().AddPamServices(new ConfigurationBuilder().Build());

    /// <summary>
    /// Every PAM-owned dependency of every PAM-registered service must itself be registered. This is the check that
    /// catches a new constructor parameter added without a matching registration — including the inert seams, which
    /// no compile step would miss.
    /// </summary>
    [Fact]
    public void AddPamServices_RegistersEveryPamOwnedDependency()
    {
        var services = PamServices();
        var registered = services.Select(d => d.ServiceType).ToHashSet();
        var pamAssembly = typeof(ServiceCollectionExtensions).Assembly;

        // The concrete types AddPamServices wires up, plus the endpoint handlers it registers by concrete type.
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
                    // Only PAM's own seams are this method's responsibility; repositories, ICurrentContext and the
                    // like are registered by the host.
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
    /// The gate override is the one registration here that has to <em>beat</em> another rather than merely exist:
    /// AddBaseServices already registers the ungating open-source default. A TryAdd here would no-op against it and
    /// leave every PAM-governed cipher fully readable — silently, with the rest of the feature working.
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
        // Every command stamps its timestamps from TimeProvider rather than DateTime.UtcNow.
        var services = PamServices();

        Assert.Contains(services, d => d.ServiceType == typeof(TimeProvider));
    }

    /// <summary>
    /// The audit emitter and the two notifiers are easy to drop by mistake — but every PAM command takes all three,
    /// so an unregistered one is a resolution failure on every PAM request.
    /// </summary>
    [Theory]
    [InlineData(typeof(IAccessAuditEventEmitter), typeof(AccessAuditEventEmitter))]
    [InlineData(typeof(IApproverInboxNotifier), typeof(ApproverInboxNotifier))]
    [InlineData(typeof(IRequesterNotifier), typeof(RequesterNotifier))]
    public void AddPamServices_RegistersSideChannelSeam(Type serviceType, Type expectedImplementation)
    {
        var services = PamServices();

        var descriptor = Assert.Single(services, d => d.ServiceType == serviceType);
        Assert.Equal(expectedImplementation, descriptor.ImplementationType);
    }

    /// <remarks>
    /// Discovered by reflection rather than listed by hand: the hand-maintained list had drifted to four of the
    /// eleven handlers PAM registers, and the sibling every-dependency test cannot catch that because it only walks
    /// registrations that already exist.
    /// </remarks>
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
        // The Minimal API endpoints resolve their handler from DI, and an unregistered handler would also make the
        // handler parameter look like a request body to Minimal API's binding.
        var services = PamServices();

        Assert.Contains(services, d => d.ServiceType == handlerType);
    }

    [Fact]
    public void EndpointHandlers_DiscoversEveryHandlerInTheAssembly()
    {
        // Guards the guard: a rename that stops matching the suffix would silently empty the theory above.
        Assert.True(EndpointHandlers().Count >= 10);
    }
}
