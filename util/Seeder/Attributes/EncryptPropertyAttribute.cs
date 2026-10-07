using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Bit.Seeder.Attributes;

/// <summary>
/// Marks a string property as Vault Data that must be encrypted.
/// Call <see cref="GetFieldPaths{T}"/> to discover all marked field paths for a type,
/// using dot notation with [*] for list elements (e.g. "login.uris[*].uri").
/// Paths are derived from [JsonPropertyName] and cached per root type.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class EncryptPropertyAttribute : Attribute
{
    private static readonly ConcurrentDictionary<Type, string[]> Cache = new();

    internal static string[] GetFieldPaths<T>() => GetFieldPaths(typeof(T));

    internal static string[] GetFieldPaths(Type rootType)
    {
        return Cache.GetOrAdd(rootType, static type =>
        {
            var paths = new List<string>();
            CollectPaths(type, prefix: "", paths, visited: []);
            return paths.ToArray();
        });
    }

    private static void CollectPaths(Type type, string prefix, List<string> paths, HashSet<Type> visited)
    {
        if (!visited.Add(type))
        {
            return;
        }

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (!prop.CanRead)
            {
                continue;
            }

            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
            var fullPath = string.IsNullOrEmpty(prefix) ? jsonName : $"{prefix}.{jsonName}";

            if (prop.GetCustomAttribute<EncryptPropertyAttribute>() is not null
                && prop.PropertyType == typeof(string))
            {
                paths.Add(fullPath);
                continue;
            }

            var propType = prop.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

            if (IsListOf(underlyingType, out var elementType)
                && elementType is not null
                && elementType.IsClass
                && elementType != typeof(string)
                && elementType != typeof(object))
            {
                CollectPaths(elementType, $"{fullPath}[*]", paths, visited);
            }
            else if (underlyingType.IsClass
                     && underlyingType != typeof(string)
                     && underlyingType != typeof(object))
            {
                CollectPaths(underlyingType, fullPath, paths, visited);
            }
        }

        visited.Remove(type);
    }

    private static bool IsListOf(Type type, out Type? elementType)
    {
        elementType = null;

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
        {
            return false;
        }

        elementType = type.GetGenericArguments()[0];
        return true;

    }
}
