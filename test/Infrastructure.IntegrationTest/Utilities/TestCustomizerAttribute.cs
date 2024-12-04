using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public class CustomizationContext
{
    // Defaults to Database.Enabled if left as null
    public bool? Enabled { get; set; }
    public Database Database { get; }

    public MethodInfo TestMethod { get; }

    public DisposalTracker DisposalTracker { get; }

    public IServiceCollection Services { get; }

    public Func<IServiceProvider, ParameterInfo, object?> ParameterResolver { get; set; } = DefaultParameterResolver;


    public CustomizationContext(Database database, MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        Database = database;
        TestMethod = testMethod;
        DisposalTracker = disposalTracker;
        Services = new ServiceCollection();
    }

    private static object? DefaultParameterResolver(IServiceProvider services, ParameterInfo parameter)
    {
        return services.GetService(parameter.ParameterType);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Assembly)]
public abstract class TestCustomizerAttribute : Attribute
{
    public abstract Task CustomizeAsync(CustomizationContext customizationContext);
}
