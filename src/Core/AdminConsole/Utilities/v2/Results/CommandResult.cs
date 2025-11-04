using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.Utilities.v2.Results;

/// <summary>
/// Represents the result of a command.
/// This is a <see cref="OneOf{Error, T}"/> type that contains an Error if the command execution failed, or the result of the command if it succeeded.
/// </summary>
/// <typeparam name="T">The type of the successful result. If there is no successful result (void), use <see cref="BulkCommandResult"/>.</typeparam>

public class CommandResult<T>(OneOf<Error, T> result) : OneOfBase<Error, T>(result)
{
    public bool IsError => IsT0;
    public bool IsSuccess => IsT1;
    public Error AsError => AsT0;
    public T AsSuccess => AsT1;

    public static implicit operator CommandResult<T>(T value) => new(value);
    public static implicit operator CommandResult<T>(Error error) => new(error);
}

/// <summary>
/// Represents the result of a command where successful execution returns no value (void).
/// See <see cref="CommandResult{T}"/> for more information.
/// </summary>
public class CommandResult(OneOf<Error, None> result) : CommandResult<None>(result)
{
    public static implicit operator CommandResult(None none) => new(none);
    public static implicit operator CommandResult(Error error) => new(error);
}

/// <summary>
/// A wrapper for <see cref="CommandResult{T}"/> with an ID, to identify the result in bulk operations.
/// </summary>
public record BulkCommandResult<T>(Guid Id, CommandResult<T> Result);

/// <summary>
/// A wrapper for <see cref="CommandResult"/> with an ID, to identify the result in bulk operations.
/// </summary>
public record BulkCommandResult(Guid Id, CommandResult Result);

public record AggregateCommandResult(IEnumerable<CommandResult> Results)
{
    public static implicit operator AggregateCommandResult(CommandResult result) => new([result]);
}

public record AggregateCommandResult<T>(IEnumerable<CommandResult<T>> Results)
{
    public static implicit operator AggregateCommandResult<T>(CommandResult<T> result) => new([result]);
}

public static class CommandResultFunctions
{
    public static Task<CommandResult<T>> ToCommandResultAsync<T>(this T value) =>
        Task.FromResult<CommandResult<T>>(value);

    public static async Task<CommandResult<TOut>> MapAsync<TIn, TOut>(
        this Task<CommandResult<TIn>> inputTask,
        Func<TIn, Task<CommandResult<TOut>>> next) =>
        await (await inputTask).Match(
            error => Task.FromResult<CommandResult<TOut>>(error),
            next);

    public static async Task<CommandResult> ToResultAsync<T>(
        this Task<CommandResult<T>> inputTask) =>
        (await inputTask).Match<CommandResult>(
            error => error,
            _ => new None());

    public static async Task<AggregateCommandResult> ApplyAsync<T>(
        this Task<CommandResult<T>> inputTask,
        IEnumerable<Func<T, Task<CommandResult>>> nextFunctions) =>
        await (await inputTask).Match(
            error => Task.FromResult(new AggregateCommandResult([error])),
            async success => new AggregateCommandResult(
                await Task.WhenAll(nextFunctions.Select(f => f(success)))));

    public static async Task<AggregateCommandResult<TOut>> ApplyAsync<TIn, TOut>(
        this Task<CommandResult<TIn>> inputTask,
        IEnumerable<Func<TIn, Task<CommandResult<TOut>>>> nextFunctions) =>
        await (await inputTask).Match(
            error => Task.FromResult(new AggregateCommandResult<TOut>([error])),
            async success => new AggregateCommandResult<TOut>(
                await Task.WhenAll(nextFunctions.Select(f => f(success)))));

    public static async Task<AggregateCommandResult> TraverseAsync<T>(
        this IEnumerable<T> items,
        Func<T, Task<CommandResult>> func) =>
        new(await Task.WhenAll(items.Select(func)));

    public static async Task<AggregateCommandResult<TOut>> TraverseAsync<TIn, TOut>(
        this IEnumerable<TIn> items,
        Func<TIn, Task<CommandResult<TOut>>> func) =>
        new(await Task.WhenAll(items.Select(func)));

    public static AggregateCommandResult<TOut> Bind<TIn, TOut>(
        this AggregateCommandResult<TIn> aggregate,
        Func<TIn, CommandResult<TOut>> selector) =>
        new(
            aggregate.Results.Select(result => result.Match<CommandResult<TOut>>(
                error => error,
                selector)));

    public static async Task<AggregateCommandResult<TOut>> BindAsync<TIn, TOut>(
        this AggregateCommandResult<TIn> aggregate,
        Func<TIn, Task<CommandResult<TOut>>> selector) =>
        new(await Task.WhenAll(aggregate.Results.Select(result =>
                result.Match(
                    error => Task.FromResult<CommandResult<TOut>>(error),
                    selector))));

    public static AggregateCommandResult<TOut> Map<TIn, TOut>(
        this AggregateCommandResult<TIn> aggregate,
        Func<TIn, TOut> mapper) =>
        new(
            aggregate.Results.Select(result => result.Match<CommandResult<TOut>>(
                error => error,
                success => mapper(success))));

    public static TResult Fold<T, TResult>(
        this AggregateCommandResult<T> aggregate,
        TResult seed,
        Func<TResult, CommandResult<T>, TResult> folder) =>
        aggregate.Results.Aggregate(seed, folder);

    public static (IEnumerable<Error> Errors, IEnumerable<T> Successes) Partition<T>(
        this AggregateCommandResult<T> aggregate) =>
        aggregate.Results.Aggregate(
            (Errors: Enumerable.Empty<Error>(), Successes: Enumerable.Empty<T>()),
            (acc, result) => result.Match(
                error => (acc.Errors.Append(error), acc.Successes),
                success => (acc.Errors, acc.Successes.Append(success))));

    public static bool AllSuccess<T>(this AggregateCommandResult<T> aggregate) =>
        aggregate.Results.All(r => r.IsSuccess);

    public static bool AnyError<T>(this AggregateCommandResult<T> aggregate) =>
        aggregate.Results.Any(r => r.IsError);

    public static async Task<CommandResult> FoldAsync<T>(
        this Task<AggregateCommandResult<T>> aggregateTask,
        string separator = "; ")
    {
        var aggregate = await aggregateTask;

        if (aggregate.AllSuccess())
        {
            return new None();
        }

        return aggregate.Results
            .Where(r => r.IsError)
            .Select(e => e.AsError)
            .ToArray()
            .Fold(separator);
    }
}
