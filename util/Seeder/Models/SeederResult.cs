#nullable enable

namespace Bit.Seeder.Models;

/// <summary>
/// Result of a seeder operation
/// </summary>
public class SeederResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    
    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static SeederResult CreateSuccess(params (string key, object value)[] data)
    {
        var result = new SeederResult { Success = true };
        foreach (var (key, value) in data)
        {
            result.Data[key] = value;
        }
        return result;
    }
    
    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static SeederResult CreateFailure(string errorMessage, Exception? exception = null)
    {
        return new SeederResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}

/// <summary>
/// Result with typed data
/// </summary>
public class SeederResult<T> : SeederResult
{
    public T? Value { get; set; }
    
    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    public static SeederResult<T> CreateSuccess(T value)
    {
        return new SeederResult<T>
        {
            Success = true,
            Value = value
        };
    }
    
    /// <summary>
    /// Creates a failure result
    /// </summary>
    public new static SeederResult<T> CreateFailure(string errorMessage, Exception? exception = null)
    {
        return new SeederResult<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}