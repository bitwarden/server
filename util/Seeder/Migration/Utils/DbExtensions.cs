using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Utils;

/// <summary>
/// Extension methods for database operations with improved null safety.
/// </summary>
public static class DbExtensions
{
    /// <summary>
    /// Executes a scalar query and returns the result with proper null handling.
    /// Safely handles null and DBNull.Value results without throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The expected return type</typeparam>
    /// <param name="command">The database command to execute</param>
    /// <param name="defaultValue">The default value to return if result is null/DBNull</param>
    /// <param name="logger">Optional logger for warnings</param>
    /// <param name="context">Optional context for logging (e.g., "table count for Users")</param>
    /// <returns>The scalar value cast to type T, or defaultValue if null/DBNull</returns>
    public static T GetScalarValue<T>(
        this DbCommand command,
        T defaultValue = default!,
        ILogger? logger = null,
        string? context = null)
    {
        var result = command.ExecuteScalar();

        if (result == null || result == DBNull.Value)
        {
            if (logger != null && !string.IsNullOrEmpty(context))
            {
                logger.LogDebug("Query returned null for {Context}, using default value", context);
            }
            return defaultValue;
        }

        try
        {
            // Handle direct cast if types match
            if (result is T typedResult)
            {
                return typedResult;
            }

            // Handle conversion for compatible types
            return (T)Convert.ChangeType(result, typeof(T));
        }
        catch (InvalidCastException ex)
        {
            if (logger != null)
            {
                logger.LogWarning(
                    "Could not cast result to {TargetType} for {Context}. Result type: {ActualType}. Error: {Error}",
                    typeof(T).Name,
                    context ?? "query",
                    result.GetType().Name,
                    ex.Message
                );
            }
            return defaultValue;
        }
        catch (FormatException ex)
        {
            if (logger != null)
            {
                logger.LogWarning(
                    "Format error converting result to {TargetType} for {Context}. Value: {Value}. Error: {Error}",
                    typeof(T).Name,
                    context ?? "query",
                    result,
                    ex.Message
                );
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// Safely attempts to rollback a transaction, catching and logging any errors.
    /// This prevents rollback errors from masking the original exception.
    /// </summary>
    /// <param name="transaction">The transaction to rollback</param>
    /// <param name="connection">The database connection (used to check if still open)</param>
    /// <param name="logger">Logger for warnings</param>
    /// <param name="context">Context for logging (e.g., table name)</param>
    public static void SafeRollback(
        this DbTransaction transaction,
        DbConnection? connection,
        ILogger logger,
        string? context = null)
    {
        try
        {
            if (connection?.State == ConnectionState.Open)
            {
                transaction.Rollback();
                if (!string.IsNullOrEmpty(context))
                {
                    logger.LogDebug("Transaction rolled back for {Context}", context);
                }
            }
            else
            {
                logger.LogWarning(
                    "Could not rollback transaction for {Context}: connection is {State}",
                    context ?? "operation",
                    connection?.State.ToString() ?? "null"
                );
            }
        }
        catch (Exception rollbackEx)
        {
            logger.LogWarning(
                rollbackEx,
                "Error during transaction rollback for {Context}: {Message}",
                context ?? "operation",
                rollbackEx.Message
            );
        }
    }
}
