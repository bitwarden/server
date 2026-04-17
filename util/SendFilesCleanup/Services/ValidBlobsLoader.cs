using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Bit.SendFilesCleanup.Services;

public class ValidBlobsLoader
{
    private const string Query = @"
SELECT LOWER(CAST(s.Id AS NVARCHAR(36))) + '/' + JSON_VALUE(s.Data, '$.Id') AS BlobPath
FROM [dbo].[Send] s
WHERE s.[Type] = 1
  AND JSON_VALUE(s.Data, '$.Id') IS NOT NULL";

    private readonly string _connectionString;
    private readonly ILogger<ValidBlobsLoader> _logger;

    public ValidBlobsLoader(string connectionString, ILogger<ValidBlobsLoader> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<HashSet<string>> LoadAsync(string outputTxtPath, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(Query, cancellationToken: ct));

        var validBlobs = new HashSet<string>(StringComparer.Ordinal);
        await using var writer = new StreamWriter(outputTxtPath);
        foreach (var path in rows)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }
            if (validBlobs.Add(path))
            {
                await writer.WriteLineAsync(path);
            }
        }

        _logger.LogInformation("Valid Blobs loaded: {Count} valid blob paths", validBlobs.Count);
        return validBlobs;
    }
}
