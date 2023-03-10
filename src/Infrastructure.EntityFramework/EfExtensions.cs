using Microsoft.EntityFrameworkCore;

namespace Bit.Infrastructure.EntityFramework;

public static class EfExtensions
{
    public static T AttachToOrGet<T>(this DbContext context, Func<T, bool> predicate, Func<T> factory)
        where T : class, new()
    {
        var match = context.Set<T>().Local.FirstOrDefault(predicate);
        if (match == null)
        {
            match = factory();
            context.Attach(match);
        }

        return match;
    }
}
