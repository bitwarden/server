namespace Bit.Seeder;

/// <summary>
/// Base interface for query operations in the seeding system. The base interface should not be used directly, rather use `IQuery&lt;TRequest, TResult&gt;`.
/// </summary>
/// <remarks>
/// Queries are synchronous, read-only operations that retrieve data from the seeding context.
/// Unlike scenes which create data, queries fetch existing data based on request parameters.
/// They follow a type-safe pattern using generics to ensure proper request/response handling
/// while maintaining a common non-generic interface for dynamic invocation.
/// </remarks>
public interface IQuery
{
    /// <summary>
    /// Gets the type of request this query expects.
    /// </summary>
    /// <returns>The request type that this query can process.</returns>
    Type GetRequestType();

    /// <summary>
    /// Executes the query based on the provided request object.
    /// </summary>
    /// <param name="request">The request object containing parameters for the query operation.</param>
    /// <returns>The query result data as an object.</returns>
    object Execute(object request);
}

/// <summary>
/// Generic query interface for synchronous, read-only operations with specific request and result types.
/// </summary>
/// <typeparam name="TRequest">The type of request object this query accepts.</typeparam>
/// <typeparam name="TResult">The type of data this query returns.</typeparam>
/// <remarks>
/// Use this interface when you need to retrieve existing data from the seeding context based on
/// specific request parameters. Queries are synchronous and do not modify data - they only read
/// and return information. The explicit interface implementations allow dynamic invocation while
/// maintaining type safety in the implementation.
/// </remarks>
public interface IQuery<TRequest, TResult> : IQuery where TRequest : class where TResult : class
{
    /// <summary>
    /// Executes the query based on the provided strongly-typed request and returns typed result data.
    /// </summary>
    /// <param name="request">The request object containing parameters for the query operation.</param>
    /// <returns>The typed query result data.</returns>
    TResult Execute(TRequest request);

    /// <summary>
    /// Gets the request type for this query.
    /// </summary>
    /// <returns>The type of TRequest.</returns>
    Type IQuery.GetRequestType() => typeof(TRequest);

    /// <summary>
    /// Adapts the non-generic Execute to the strongly-typed version.
    /// </summary>
    /// <param name="request">The request object to cast and process.</param>
    /// <returns>The typed result cast to object.</returns>
    object IQuery.Execute(object request) => Execute((TRequest)request);
}
