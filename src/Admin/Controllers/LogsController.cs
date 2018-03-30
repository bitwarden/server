using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Admin.Models;
using Bit.Core;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

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

        public async Task<IActionResult> Index(string cursor = null, int count = 25)
        {
            var collectionLink = UriFactory.CreateDocumentCollectionUri(Database, Collection);
            using(var client = new DocumentClient(new Uri(_globalSettings.DocumentDb.Uri),
                _globalSettings.DocumentDb.Key))
            {
                var options = new FeedOptions
                {
                    MaxItemCount = count,
                    RequestContinuation = cursor
                };

                var query = client.CreateDocumentQuery(collectionLink, options)
                    .OrderByDescending(l => l.Timestamp).AsDocumentQuery();
                var response = await query.ExecuteNextAsync<LogModel>();

                return View(new CursorPagedModel<LogModel>
                {
                    Items = response.ToList(),
                    Count = count,
                    Cursor = cursor,
                    NextCursor = response.ResponseContinuation
                });
            }
        }

        public async Task<IActionResult> View(string id)
        {
            using(var client = new DocumentClient(new Uri(_globalSettings.DocumentDb.Uri),
                _globalSettings.DocumentDb.Key))
            {
                var uri = UriFactory.CreateDocumentUri(Database, Collection, id);
                var response = await client.ReadDocumentAsync<LogModel>(uri);
                if(response?.Document == null)
                {
                    return RedirectToAction("Index");
                }

                return View(response.Document);
            }
        }
    }
}
