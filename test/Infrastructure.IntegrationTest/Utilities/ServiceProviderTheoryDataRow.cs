using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public class ServiceTheoryDataRow : TheoryDataRowBase
{
    private readonly MethodInfo _testMethod;
    private readonly DisposalTracker _disposalTracker;
    private readonly CustomizationContext _customizationContext;

    public ServiceTheoryDataRow(MethodInfo testMethod, DisposalTracker disposalTracker, CustomizationContext customizationContext)
    {
        _testMethod = testMethod;
        _disposalTracker = disposalTracker;
        _customizationContext = customizationContext;
    }

    protected override object?[] GetData()
    {
        Console.WriteLine($"Traits:\n{Traits.Select((k, v) => $"{k}: {string.Join(", ", v)}\n")}");
        var parameters = _testMethod.GetParameters();

        var sp = _customizationContext.Services.BuildServiceProvider();

        // Create a scope for this test
        var scope = sp.CreateAsyncScope();

        var objects = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            objects[i] = _customizationContext.ParameterResolver(scope.ServiceProvider, parameter);
        }

        _disposalTracker.Add(scope);
        _disposalTracker.Add(sp);

        return objects;
    }
}
