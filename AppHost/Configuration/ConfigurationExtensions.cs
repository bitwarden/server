using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

namespace Bit.AppHost.Configuration;

/// <summary>
/// Binds the AppHost configuration to <see cref="AppHostSettings"/> after checking that every required key is present.
/// </summary>
internal static class ConfigurationExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// The bound settings. Each read binds the configuration again, so read it once per method.
        /// </summary>
        /// <exception cref="InvalidOperationException">A required key is missing or a value cannot be converted.</exception>
        internal AppHostSettings Settings
        {
            get
            {
                RequireKeys(builder.Configuration, typeof(AppHostSettings), prefix: null);
                // The settings properties are internal, and the binder skips non-public properties by default.
                return builder.Configuration.Get<AppHostSettings>(options => options.BindNonPublicProperties = true)!;
            }
        }
    }

    /// <summary>
    /// Throws when a <c>required</c> property of <paramref name="type"/> has no configuration key. Recurses into
    /// settings records and dictionary values; a dictionary keyed by an enum needs an entry for every member.
    /// </summary>
    /// <remarks>
    /// The binder ignores <c>required</c> and leaves missing values at their defaults, so this check runs first.
    /// </remarks>
    private static void RequireKeys(IConfiguration configuration, Type type, string? prefix)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var key = prefix is null ? property.Name : $"{prefix}:{property.Name}";
            var section = configuration.GetSection(key);
            if (!section.Exists())
            {
                if (property.IsDefined(typeof(RequiredMemberAttribute)))
                {
                    throw new InvalidOperationException($"Missing required configuration: {key}");
                }

                continue;
            }

            var propertyType = property.PropertyType;
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var (keyType, valueType) = (propertyType.GenericTypeArguments[0], propertyType.GenericTypeArguments[1]);
                var entryKeys = keyType.IsEnum
                    ? Enum.GetNames(keyType).Select(name => $"{key}:{name}")
                    : section.GetChildren().Select(child => child.Path);
                foreach (var entryKey in entryKeys)
                {
                    if (!configuration.GetSection(entryKey).Exists())
                    {
                        throw new InvalidOperationException($"Missing required configuration: {entryKey}");
                    }

                    RequireKeys(configuration, valueType, entryKey);
                }
            }
            else if (propertyType.IsClass && propertyType != typeof(string) && !propertyType.IsArray)
            {
                RequireKeys(configuration, propertyType, key);
            }
        }
    }
}
