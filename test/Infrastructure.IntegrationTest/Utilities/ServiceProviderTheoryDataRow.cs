using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public class ServiceTheoryDataRow : TheoryDataRowBase
{
    private readonly MethodInfo _testMethod;
    private readonly DisposalTracker _disposalTracker;
    private readonly IServiceProvider _serviceProvider;

    public ServiceTheoryDataRow(MethodInfo testMethod, DisposalTracker disposalTracker, IServiceProvider serviceProvider)
    {
        _testMethod = testMethod;
        _disposalTracker = disposalTracker;
        _serviceProvider = serviceProvider;
    }

    protected override object?[] GetData()
    {
        var parameters = _testMethod.GetParameters();
        // Create a scope for this test
        var scope = _serviceProvider.CreateAsyncScope();

        var objects = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            objects[i] = scope.ServiceProvider.GetRequiredService(parameters[i].ParameterType);
        }

        _disposalTracker.Add(scope);

        return objects;
    }
}
