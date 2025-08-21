using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Health;

public class ActivityDecoratedProxy<TDecorated> : DispatchProxy
{

    private ActivitySource? _activity;
    private TDecorated? _decorated;

    public static TDecorated Create(TDecorated decorated, IServiceProvider serviceProvider)
    {
        var activity = serviceProvider.GetRequiredService<ActivitySource>();
        object proxy = Create<TDecorated, ActivityDecoratedProxy<TDecorated>>()!;
        ((ActivityDecoratedProxy<TDecorated>)proxy!).SetParameters(decorated, activity);

        return (TDecorated)proxy;
    }

    private void SetParameters(TDecorated decorated, ActivitySource activitySource)
    {
        _decorated = decorated;
        _activity = activitySource;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_activity == null || _decorated == null || targetMethod == null)
        {
            throw new InvalidOperationException("Decorator is not properly initialized.");
        }

        using var activity = _activity.StartActivity($"{_decorated.GetType().FullName}.{targetMethod.Name}");
        try
        {
            var result = targetMethod.Invoke(_decorated, args);
            return result;
        }
        catch (Exception e)
        {
            activity?.AddException(e);
            throw;

        }
    }
}
