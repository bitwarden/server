using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Bit.Billing.Test.Utilities;

/// <summary>
/// Builds a <see cref="SqlException"/> with a given error number. SqlException has no public constructor, so this uses
/// the same internal factory SqlClient uses.
/// </summary>
public static class SqlExceptionFactory
{
    public static SqlException Create(int number)
    {
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;

        var errorConstructor = typeof(SqlError)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var arguments = errorConstructor.GetParameters().Select(p => p.Name switch
        {
            "infoNumber" => number,
            "errorClass" => (byte)14,
            "state" => (byte)1,
            "server" or "errorMessage" or "procedure" => (object)$"SQL error {number}",
            "lineNumber" => 0,
            "win32ErrorCode" => p.ParameterType == typeof(uint) ? (object)0u : 0,
            "batchIndex" => -1,
            _ => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null
        }).ToArray();
        var error = (SqlError)errorConstructor.Invoke(arguments);

        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(errors, [error]);

        var create = typeof(SqlException)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name == "CreateException")
            .OrderBy(m => m.GetParameters().Length)
            .First(m => m.GetParameters()[0].ParameterType == typeof(SqlErrorCollection));
        var createArguments = create.GetParameters()
            .Select((p, i) => i == 0 ? errors : p.ParameterType == typeof(string) ? "16.0.0" : p.HasDefaultValue ? p.DefaultValue : null)
            .ToArray();

        return (SqlException)create.Invoke(null, createArguments)!;
    }
}
