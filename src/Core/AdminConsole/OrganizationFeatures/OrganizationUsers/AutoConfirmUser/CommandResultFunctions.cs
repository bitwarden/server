using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public static class CommandResultFunctions
{
    public static Task<CommandResult<T>> ToCommandResultAsync<T>(this T value)
    {
        return Task.FromResult<CommandResult<T>>(value);
    }

    public static async Task<CommandResult<TOut>> MapAsync<TIn, TOut>(
        this Task<CommandResult<TIn>> inputTask,
        Func<TIn, Task<CommandResult<TOut>>> next)
    {
        var input = await inputTask;
        if (input.IsError) return input.AsError;
        return await next(input.AsSuccess);
    }

    public static async Task<CommandResult> ToResultAsync<T>(
        this Task<CommandResult<T>> inputTask)
    {
        var input = await inputTask;
        if (input.IsError) return input.AsError;
        return new None();
    }
}
