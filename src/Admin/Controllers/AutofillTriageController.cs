#nullable enable

using Bit.Admin.Enums;
using Bit.Admin.Models;
using Bit.Admin.Utilities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Admin.Controllers;

[Authorize]
public class AutofillTriageController : Controller
{
    private readonly IAutofillTriageReportRepository _repo;

    public AutofillTriageController(IAutofillTriageReportRepository repo)
        => _repo = repo;

    [RequirePermission(Permission.User_List_View)]
    public async Task<IActionResult> Index(int page = 1, int count = 25)
    {
        var skip = (page - 1) * count;
        var reports = await _repo.GetActiveAsync(skip, count);
        var model = new AutofillTriageModel
        {
            Items = reports.ToList(),
            Page = page,
            Count = count,
        };
        return View(model);
    }

    [RequirePermission(Permission.User_List_View)]
    public async Task<IActionResult> Details(Guid id)
    {
        var report = await _repo.GetByIdAsync(id);
        if (report is null)
        {
            return NotFound();
        }

        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permission.User_List_View)]
    public async Task<IActionResult> Archive(Guid id)
    {
        await _repo.ArchiveAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
