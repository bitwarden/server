#nullable enable

using System.Linq.Expressions;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public delegate bool IntegrationFilter(EventMessage message, object? value);

public static class IntegrationFilterFactory
{
    public static IntegrationFilter BuildEqualityFilter<T>(string propertyName)
    {
        var param = Expression.Parameter(typeof(EventMessage), "m");
        var valueParam = Expression.Parameter(typeof(object), "val");

        var property = Expression.PropertyOrField(param, propertyName);
        var typedVal = Expression.Convert(valueParam, typeof(T));
        var body = Expression.Equal(property, typedVal);

        var lambda = Expression.Lambda<Func<EventMessage, object?, bool>>(body, param, valueParam);
        return new IntegrationFilter(lambda.Compile());
    }

    public static IntegrationFilter BuildInFilter<T>(string propertyName)
    {
        var param = Expression.Parameter(typeof(EventMessage), "m");
        var valueParam = Expression.Parameter(typeof(object), "val");

        var property = Expression.PropertyOrField(param, propertyName);

        var method = typeof(Enumerable)
            .GetMethods()
            .FirstOrDefault(m =>
                m.Name == "Contains"
                && m.GetParameters().Length == 2)
            ?.MakeGenericMethod(typeof(T));
        if (method is null)
        {
            throw new InvalidOperationException("Could not find Contains method.");
        }

        var listType = typeof(IEnumerable<T>);
        var castedList = Expression.Convert(valueParam, listType);

        var containsCall = Expression.Call(method, castedList, property);

        var lambda = Expression.Lambda<Func<EventMessage, object?, bool>>(containsCall, param, valueParam);
        return new IntegrationFilter(lambda.Compile());
    }
}
