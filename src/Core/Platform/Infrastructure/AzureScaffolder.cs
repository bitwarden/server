#nullable enable

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Infrastructure;

internal class BrokenDevelepmentEnvironmentException : Exception
{
    public BrokenDevelepmentEnvironmentException(string message)
        : base (message)
    {
        
    }

    public BrokenDevelepmentEnvironmentException(string message, Exception innerException)
        : base(message, innerException)
    {

    }
}

public class AzureScaffolder : IHostedService
{
    private static readonly IEnumerable<string> _containers =
      [ "attachments", "sendfiles", "misc" ];

    private static readonly IEnumerable<string> _queues =
      [ "event", "notifications", "reference-events", "mail" ];

    private static readonly IEnumerable<string> _tables =
      [ "event", "metadata", "installationdevice"];

    private static readonly BlobCorsRule _corsRule = new()
    {
        AllowedHeaders = "*",
        ExposedHeaders = "*",
        AllowedOrigins = "*",
        MaxAgeInSeconds = 30,
        AllowedMethods = "GET,PUT"
    };

    private static readonly EqualityComparer<BlobCorsRule> _corsComparer = EqualityComparer<BlobCorsRule>.Create((a, b) =>
    {
        if (a == null)
        {
            return b == null;
        }

        if (b == null)
        {
            return false;
        }

        return a.AllowedOrigins == b.AllowedOrigins
            && a.AllowedMethods == b.AllowedMethods
            && a.AllowedHeaders == b.AllowedHeaders
            && a.ExposedHeaders == b.ExposedHeaders
            && a.MaxAgeInSeconds == b.MaxAgeInSeconds;
    });

    private static readonly string _developmentUri = "UseDevelopmentStorage=true";
    private readonly ILogger<AzureScaffolder> _logger;

    public AzureScaffolder(ILogger<AzureScaffolder> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Scaffolding Azurite Infrastrucure.");
            await ScaffoldAsync(cancellationToken);
        }
        // TODO: Handle certain errors with instructions on how to fix, like API version problems
        catch (RequestFailedException requestedFailedEx) when (requestedFailedEx.ErrorCode == "InvalidHeaderValue")
        {
            // Rethrow with more explicit exception
            throw new BrokenDevelepmentEnvironmentException(
                "The version of Azurite being ran is incompitable with our Azure packages. Read more https://contributing.bitwarden.com/link-here", 
                requestedFailedEx
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unknown error while scaffolding Azure infrastructure in Azurite.");
            throw;
        }
    }

    private static async Task ScaffoldAsync(CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(_developmentUri);
        
        foreach (var container in _containers)
        {
            var blobContainer = blobServiceClient.GetBlobContainerClient(container);
            if (!await blobContainer.ExistsAsync(cancellationToken))
            {
                await blobContainer.CreateAsync(cancellationToken: cancellationToken);
            }
        }


        // Check if our cors rule is already added, if not, add it.
        var properties = await blobServiceClient.GetPropertiesAsync(cancellationToken);
        var existingCorsRules = properties.Value.Cors;
        if (!existingCorsRules.Contains(_corsRule, _corsComparer))
        {
            properties.Value.Cors.Add(_corsRule);
            await blobServiceClient.SetPropertiesAsync(properties.Value, cancellationToken);
        }

        var queueServiceClient = new QueueServiceClient(_developmentUri);

        foreach (var queue in _queues)
        {
            var queueClient = queueServiceClient.GetQueueClient(queue);
            if (!await queueClient.ExistsAsync(cancellationToken))
            {
                await queueClient.CreateAsync(cancellationToken: cancellationToken);
            }
        }
        
        var tableServiceClient = new TableServiceClient(_developmentUri);
        
        foreach (var table in _tables)
        {
            var tableClient = tableServiceClient.GetTableClient(table);
            await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
