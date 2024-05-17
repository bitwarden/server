namespace Bit.Core.AdminConsole.Extensions;

public static class TaskExtensions
{
    public async static Task<TResult> Then<T, TResult>(this Task<T> source, Func<T, Task<TResult>> selector)
    {
        T x = await source;
        return await selector(x);
    }
}
