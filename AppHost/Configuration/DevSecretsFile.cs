using System.Data.Common;
using Bit.AppHost.Configuration.Databases;
using Microsoft.Extensions.Configuration;

namespace Bit.AppHost.Configuration;

/// <summary>
/// The database connection strings in <c>dev/secrets.json</c>.
/// </summary>
internal sealed class DevSecretsFile
{
    private const string _selfHostOverridePrefix = "dev:selfHostOverride:";

    private readonly IConfiguration _secrets;

    private readonly string _path;

    private DevSecretsFile(IConfiguration secrets, string path)
    {
        _secrets = secrets;
        _path = path;
    }

    /// <summary>
    /// Reads the JSON file at <paramref name="path"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The file does not exist.</exception>
    internal static DevSecretsFile Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"{path} not found. Copy dev/secrets.json.example to it and fill in the connection strings.");
        }

        return new DevSecretsFile(new ConfigurationBuilder().AddJsonFile(path).Build(), path);
    }

    /// <summary>
    /// The password in <paramref name="engine"/>'s connection string. In self-hosted mode, the
    /// <c>dev:selfHostOverride</c> connection string is read first when it is set.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The connection string is missing, malformed or has no password. Messages name the key, never the value.
    /// </exception>
    internal string GetDatabasePassword(Engine engine, bool selfHost)
    {
        var key = engine.ConnectionStringKey;
        if (selfHost && !string.IsNullOrWhiteSpace(_secrets[_selfHostOverridePrefix + key]))
        {
            key = _selfHostOverridePrefix + key;
        }

        var connectionString = _secrets[key];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw AppHostSettingsBinding.MissingConfiguration($"{key} in {_path}");
        }

        var parsed = new DbConnectionStringBuilder();
        try
        {
            parsed.ConnectionString = connectionString;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"{key} in {_path} is not a valid connection string.", exception);
        }

        if ((parsed.TryGetValue("password", out var password) || parsed.TryGetValue("pwd", out password))
            && password is string value && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"{key} in {_path} has no Password or pwd.");
    }
}
