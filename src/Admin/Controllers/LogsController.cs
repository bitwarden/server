using Bit.Admin.Models;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Serilog.Events;

namespace Bit.Admin.Controllers;

[Authorize]
[SelfHosted(NotSelfHostedOnly = true)]
public class LogsController : Controller
{
    private const string Database = "Diagnostics";
    private const string Container = "Logs";

    private readonly GlobalSettings _globalSettings;

    public LogsController(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public async Task<IActionResult> Index(string cursor = null, int count = 50,
        LogEventLevel? level = null, string project = null, DateTime? start = null, DateTime? end = null)
    {
        using (var client = new CosmosClient(_globalSettings.DocumentDb.Uri,
            _globalSettings.DocumentDb.Key))
        {
            var cosmosContainer = client.GetContainer(Database, Container);
            var query = cosmosContainer.GetItemLinqQueryable<LogModel>(
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = count
                },
                continuationToken: cursor
            ).AsQueryable();

            if (level.HasValue)
            {
                query = query.Where(l => l.Level == level.Value.ToString());
            }
            if (!string.IsNullOrWhiteSpace(project))
            {
                query = query.Where(l => l.Properties != null && l.Properties["Project"] == (object)project);
            }
            if (start.HasValue)
            {
                query = query.Where(l => l.Timestamp >= start.Value);
            }
            if (end.HasValue)
            {
                query = query.Where(l => l.Timestamp <= end.Value);
            }
            var feedIterator = query.OrderByDescending(l => l.Timestamp).ToFeedIterator();
            var response = await feedIterator.ReadNextAsync();

            return View(new LogsModel
            {
                Level = level,
                Project = project,
                Start = start,
                End = end,
                Items = response.ToList(),
                Count = count,
                Cursor = cursor,
                NextCursor = response.ContinuationToken
            });
        }
    }

    public async Task<IActionResult> View(Guid id)
    {
        using (var client = new CosmosClient(_globalSettings.DocumentDb.Uri,
            _globalSettings.DocumentDb.Key))
        {
            var cosmosContainer = client.GetContainer(Database, Container);
            var query = cosmosContainer.GetItemLinqQueryable<LogDetailsModel>()
                .AsQueryable()
                .Where(l => l.Id == id.ToString());

            var response = await query.ToFeedIterator().ReadNextAsync();
            if (response == null || response.Count == 0)
            {
                return RedirectToAction("Index");
            }
            return View(response.First());
        }
    }
}
