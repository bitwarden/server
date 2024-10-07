using Bit.Admin.Billing.Models;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core.Billing.Migration.Models;
using Bit.Core.Billing.Migration.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("migrate-providers")]
[SelfHosted(NotSelfHostedOnly = true)]
public class MigrateProvidersController(
    IProviderMigrator providerMigrator) : Controller
{
    [HttpGet]
    [RequirePermission(Permission.Tools_MigrateProviders)]
    public IActionResult Index()
    {
        return View(new MigrateProvidersRequestModel());
    }

    [HttpPost]
    [RequirePermission(Permission.Tools_MigrateProviders)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostAsync(MigrateProvidersRequestModel request)
    {
        var providerIds = GetProviderIdsFromInput(request.ProviderIds);

        if (providerIds.Count == 0)
        {
            return RedirectToAction("Index");
        }

        foreach (var providerId in providerIds)
        {
            await providerMigrator.Migrate(providerId);
        }

        return RedirectToAction("Results", new { ProviderIds = string.Join("\r\n", providerIds) });
    }

    [HttpGet("results")]
    [RequirePermission(Permission.Tools_MigrateProviders)]
    public async Task<IActionResult> ResultsAsync(MigrateProvidersRequestModel request)
    {
        var providerIds = GetProviderIdsFromInput(request.ProviderIds);

        if (providerIds.Count == 0)
        {
            return View(Array.Empty<ProviderMigrationResult>());
        }

        var results = await Task.WhenAll(providerIds.Select(providerMigrator.GetResult));

        return View(results);
    }

    [HttpGet("results/{providerId:guid}")]
    [RequirePermission(Permission.Tools_MigrateProviders)]
    public async Task<IActionResult> DetailsAsync([FromRoute] Guid providerId)
    {
        var result = await providerMigrator.GetResult(providerId);

        if (result == null)
        {
            return RedirectToAction("Index");
        }

        return View(result);
    }

    private static List<Guid> GetProviderIdsFromInput(string text) => !string.IsNullOrEmpty(text)
        ? text.Split(
                ["\r\n", "\r", "\n"],
                StringSplitOptions.TrimEntries
            )
            .Select(id => new Guid(id))
            .ToList()
        : [];
}
