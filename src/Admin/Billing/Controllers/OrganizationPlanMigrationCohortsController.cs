using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;
using Bit.Admin.Enums;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Services;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

// ReSharper disable InconsistentNaming
namespace Bit.Admin.Billing.Controllers;

[Authorize]
[Route("migration-cohorts")]
public class OrganizationPlanMigrationCohortsController(
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IStripeAdapter stripeAdapter,
    ILogger<OrganizationPlanMigrationCohortsController> logger,
    IFeatureService featureService,
    IGetCohortAssignmentStateQuery getCohortAssignmentStateQuery,
    IExportCohortAssignmentsQuery exportCohortAssignmentsQuery) : Controller
{
    private const int _defaultPageSize = 25;

    // How many CSV rows to buffer before flushing to the response stream during an export. Keeps
    // bytes flowing on large cohorts so the connection doesn't idle into a server write-timeout.
    // Intentionally independent of the export query's DB page size -- this only bounds buffering.
    private const int _exportFlushEveryRows = 1000;

    private bool PlanMigrationCohortsFeatureEnabled() =>
        featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration);

    [HttpGet("")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Index(string? name = null, int page = 1, int count = _defaultPageSize)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        if (page < 1) page = 1;
        if (count < 1) count = 1;
        var skip = (page - 1) * count;

        var items = await cohortRepository.SearchWithCountsAsync(name, skip, count);

        return View(new CohortsPagedModel
        {
            NameSearch = name,
            Items = items.Select(CohortListItemViewModel.From).ToList(),
            Page = page,
            Count = count,
        });
    }

    [HttpGet("create")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public IActionResult Create()
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        return View(new CohortFormModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Create(CohortFormModel model)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        MergeCrossFieldValidationErrors(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            if (!await ValidateNameAsync(model.Name) || !await ValidateCouponsAsync(model))
            {
                return View(model);
            }

            var cohort = new OrganizationPlanMigrationCohort
            {
                Name = model.Name,
                MigrationPathId = model.GetMigrationPathId(),
                ProactiveDiscountCouponCode = NormalizeCouponCode(model.ProactiveDiscountCouponCode),
                ChurnDiscountCouponCode = NormalizeCouponCode(model.ChurnDiscountCouponCode),
            };

            await cohortRepository.CreateAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating cohort. Name: {Name}", model.Name);
            ModelState.AddModelError(string.Empty, "An error occurred while saving the cohort.");
            return View(model);
        }
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        var assignmentState = await getCohortAssignmentStateQuery.Run(cohort);
        return View(EditCohortViewModel.From(cohort, CohortFormModel.From(cohort), assignmentState));
    }

    [HttpGet("{id:guid}/export")]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Export(Guid id)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        var fileName = BuildExportFileName(cohort.Name);

        logger.LogInformation(
            "Cohort CSV export started. Actor: {Actor}, CohortId: {CohortId}",
            User?.Identity?.Name ?? "unknown",
            cohort.Id);

        Response.ContentType = "text/csv";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false };
        var writer = new StreamWriter(Response.Body, new UTF8Encoding(false));
        var csv = new CsvWriter(writer, config);

        foreach (var header in new[]
                 {
                     "OrganizationId", "OrganizationName", "AssignedAt", "ScheduledDate", "MigratedDate",
                 })
        {
            csv.WriteField(header);
        }
        await csv.NextRecordAsync();

        var sinceFlush = 0;
        var rowsWritten = 0;
        var aborted = false;
        try
        {
            await foreach (var row in exportCohortAssignmentsQuery.GetByCohortIdAsync(cohort.Id))
            {
                csv.WriteField(row.OrganizationId.ToString());
                csv.WriteField(SanitizeCsvField(row.OrganizationName));
                csv.WriteField(row.AssignedAt.ToString("o", CultureInfo.InvariantCulture));
                csv.WriteField(row.ScheduledDate?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty);
                csv.WriteField(row.MigratedDate?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty);
                await csv.NextRecordAsync();
                rowsWritten++;

                if (++sinceFlush >= _exportFlushEveryRows)
                {
                    await csv.FlushAsync();
                    sinceFlush = 0;
                }
            }

            await csv.FlushAsync();
        }
        catch (Exception ex)
        {
            // Deliberately broad: once the 200 + headers are committed we cannot convert any failure
            // (DB read, serialization, write) into an error response, so every failure mode must take
            // the same path -- log it and abort the connection so the operator gets a visibly broken
            // download instead of a silently truncated CSV that looks complete.
            logger.LogError(ex,
                "Cohort CSV export failed mid-stream after the response started. CohortId: {CohortId}, RowsWritten: {RowsWritten}",
                cohort.Id,
                rowsWritten);
            aborted = true;
            HttpContext.Abort();
        }
        finally
        {
            try
            {
                await csv.DisposeAsync();
                await writer.DisposeAsync();
            }
            catch (Exception ex) when (aborted)
            {
                // After an abort the underlying stream is dead, so a dispose-time flush throws. The
                // original failure was already logged and surfaced via the abort; record this at debug
                // for diagnosability without masking the real cause.
                logger.LogDebug(ex, "Disposing the CSV writer after an aborted export threw; ignoring.");
            }
        }

        return new EmptyResult();
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Edit(Guid id, CohortFormModel model)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        model.Id = id;

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        var assignmentState = await getCohortAssignmentStateQuery.Run(cohort);

        if (assignmentState.HasNonPendingAssignments)
        {
            // The locked migration path view doesn't post a value for MigrationPathSelection.
            // Restore from the persisted cohort so the eventual ReplaceAsync writes back the
            // unchanged path, and clear the binding-time [Required] error so the model is valid.
            model.MigrationPathSelection = cohort.MigrationPathId switch
            {
                null => CohortFormModel.NoMigrationPath,
                var pathId => ((byte)pathId).ToString(),
            };
            ModelState.Remove(nameof(model.MigrationPathSelection));
        }

        MergeCrossFieldValidationErrors(model);

        if (!ModelState.IsValid)
        {
            return View(EditCohortViewModel.From(cohort, model, assignmentState));
        }

        try
        {
            if (!await ValidateNameAsync(model.Name, id)
                || !await ValidateCouponsAsync(model))
            {
                return View(EditCohortViewModel.From(cohort, model, assignmentState));
            }

            cohort.Name = model.Name;
            cohort.MigrationPathId = model.GetMigrationPathId();
            cohort.ProactiveDiscountCouponCode = NormalizeCouponCode(model.ProactiveDiscountCouponCode);
            cohort.ChurnDiscountCouponCode = NormalizeCouponCode(model.ChurnDiscountCouponCode);
            cohort.IsActive = model.IsActive;
            cohort.RevisionDate = DateTime.UtcNow;

            await cohortRepository.ReplaceAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating cohort. Id: {Id}", id);
            ModelState.AddModelError(string.Empty, "An error occurred while saving the cohort.");
            return View(EditCohortViewModel.From(cohort, model, assignmentState));
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.Tools_ManagePlanMigrationCohorts)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!PlanMigrationCohortsFeatureEnabled()) return NotFound();

        var cohort = await cohortRepository.GetByIdAsync(id);
        if (cohort == null) return NotFound();

        try
        {
            var assignmentState = await getCohortAssignmentStateQuery.Run(cohort);
            if (assignmentState.HasNonPendingAssignments)
            {
                TempData["Error"] =
                    $"Cannot delete cohort '{cohort.Name}' because {assignmentState.NonPendingAssignmentCount:N0} " +
                    "assignment(s) have left the Pending state. Historical migration and " +
                    "save-offer records are preserved.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            await cohortRepository.DeleteAsync(cohort);

            TempData["Success"] = $"Cohort '{cohort.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting cohort. Id: {Id}", id);
            TempData["Error"] = "An error occurred while attempting to delete the cohort.";
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    private static string? NormalizeCouponCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildExportFileName(string cohortName)
    {
        var slug = Regex.Replace(cohortName ?? string.Empty, "[^a-zA-Z0-9]+", "-")
            .Trim('-')
            .ToLowerInvariant();

        if (string.IsNullOrEmpty(slug))
        {
            slug = "cohort";
        }

        return $"{slug}-{DateTime.UtcNow:yyyy-MM-dd}.csv";
    }

    private static string SanitizeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + value
            : value;
    }

    // MVC skips IValidatableObject.Validate when any property-level attribute already failed, hiding cross-field
    // rules until the operator resubmits. Run it explicitly so every error surfaces on a single submit.
    // See https://github.com/dotnet/aspnetcore/issues/1899.
    private void MergeCrossFieldValidationErrors(CohortFormModel model)
    {
        foreach (var result in model.Validate(new ValidationContext(model)))
        {
            foreach (var memberName in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                ModelState.AddModelError(memberName, result.ErrorMessage ?? string.Empty);
            }
        }
    }

    private async Task<bool> ValidateNameAsync(string name, Guid? excludeId = null)
    {
        var existing = await cohortRepository.GetByNameAsync(name);
        if (existing == null || existing.Id == excludeId)
        {
            return true;
        }

        ModelState.AddModelError(nameof(CohortFormModel.Name), "A cohort with this name already exists.");
        return false;
    }

    private async Task<bool> ValidateCouponsAsync(CohortFormModel model)
    {
        var proactive = NormalizeCouponCode(model.ProactiveDiscountCouponCode);
        var churn = NormalizeCouponCode(model.ChurnDiscountCouponCode);

        var ok = !(proactive != null && !await TryValidateCouponAsync(proactive, nameof(model.ProactiveDiscountCouponCode)));
        if (churn != null && !await TryValidateCouponAsync(churn, nameof(model.ChurnDiscountCouponCode)))
        {
            ok = false;
        }
        return ok;
    }

    private async Task<bool> TryValidateCouponAsync(string couponId, string fieldName)
    {
        try
        {
            await stripeAdapter.GetCouponAsync(couponId);
            return true;
        }
        catch (StripeException ex)
        {
            var message = ex.StripeError?.Code == "resource_missing"
                ? "Coupon not found in Stripe. Please verify the coupon ID."
                : "An error occurred while fetching the coupon from Stripe.";

            logger.LogError(ex, "Stripe coupon error: {CouponId}", couponId);
            ModelState.AddModelError(fieldName, message);
            return false;
        }
    }

}
