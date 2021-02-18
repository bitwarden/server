using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Serilog.Events;

namespace Bit.Admin.Controllers
{
    [Authorize]
    [SelfHosted(NotSelfHostedOnly = true)]
    public class LogsController : Controller
    {
        private const string Database = "Diagnostics";
        private const string Collection = "Logs";

        private readonly GlobalSettings _globalSettings;

        public LogsController(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public async Task<IActionResult> Index(string cursor = null, int count = 50,
            LogEventLevel? level = null, string project = null, DateTime? start = null, DateTime? end = null)
        {
            var collectionLink = UriFactory.CreateDocumentCollectionUri(Database, Collection);
            using (var client = new DocumentClient(new Uri(_globalSettings.DocumentDb.Uri),
                _globalSettings.DocumentDb.Key))
            {
                var options = new FeedOptions
                {
                    MaxItemCount = count,
                    RequestContinuation = cursor
                };

                var query = client.CreateDocumentQuery<LogModel>(collectionLink, options).AsQueryable();
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

                var docQuery = query.OrderByDescending(l => l.Timestamp).AsDocumentQuery();
                var response = await docQuery.ExecuteNextAsync<LogModel>();

                return View(new LogsModel
                {
                    Level = level,
                    Project = project,
                    Start = start,
                    End = end,
                    Items = response.ToList(),
                    Count = count,
                    Cursor = cursor,
                    NextCursor = response.ResponseContinuation
                });
            }
        }

        public async Task<IActionResult> View(Guid id)
        {
            using (var client = new DocumentClient(new Uri(_globalSettings.DocumentDb.Uri),
                _globalSettings.DocumentDb.Key))
            {
                var uri = UriFactory.CreateDocumentUri(Database, Collection, id.ToString());
                var response = await client.ReadDocumentAsync<LogDetailsModel>(uri);
                if (response?.Document == null)
                {
                    return RedirectToAction("Index");
                }

                return View(response.Document);
            }
        }
    }
}
