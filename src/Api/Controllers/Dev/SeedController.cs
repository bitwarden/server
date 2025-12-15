using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Recipes;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers.Dev;

[ApiController]
[Route("dev/seed")]
[ApiExplorerSettings(IgnoreApi = true)]
public class SeedController : ControllerBase
{
    private readonly DatabaseContext _db;
    private readonly ILogger<SeedController> _logger;

    public SeedController(DatabaseContext db, ILogger<SeedController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    public class OrgWithUsersRequest
    {
        public string Label { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Domain { get; set; } = null!;
        public int UserCount { get; set; }
        public bool Replace { get; set; }
    }

    public class OrgWithUsersResponse
    {
        public Guid OrganizationId { get; set; }
        public string AdminEmail { get; set; } = null!;
        public string AdminPassword { get; set; } = null!;
    }

    [HttpPost("organization-with-users")]
    public IActionResult OrganizationWithUsers([FromBody] OrgWithUsersRequest request)
    {
        if (request.Replace)
        {
            PurgeByLabelInternal(request.Label, out _, out _);
        }

        var labeledName = request.Name;
        var adminEmail = $"seed-{request.Label}-admin@{request.Domain}";
        var recipe = new OrganizationWithUsersRecipe(_db);
        var orgId = recipe.Seed(labeledName, request.Domain, request.UserCount, label: request.Label);
        // Seeders create fixed password hash; return the known plaintext for automation
        var adminPassword = "P@ssword123!";
        _logger.LogInformation("Seeded org {OrgId} with label {Label} from {Ip}", orgId, request.Label, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new OrgWithUsersResponse { OrganizationId = orgId, AdminEmail = adminEmail, AdminPassword = adminPassword });
    }

    public class PurgeRequest { public string Label { get; set; } = null!; public bool Confirm { get; set; } }
    public class PurgeResponse { public int UsersDeleted { get; set; } public int OrganizationsDeleted { get; set; } }

    [HttpPost("purge")]
    public IActionResult Purge([FromBody] PurgeRequest request)
    {
        if (!request.Confirm) { return BadRequest(new { error = "confirm=true required" }); }
        PurgeByLabelInternal(request.Label, out var users, out var orgs);
        _logger.LogInformation("Purged label {Label}: users={Users} orgs={Orgs} from {Ip}", request.Label, users, orgs, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new PurgeResponse { UsersDeleted = users, OrganizationsDeleted = orgs });
    }

    private void PurgeByLabelInternal(string label, out int usersDeleted, out int orgsDeleted)
    {
        usersDeleted = 0; orgsDeleted = 0;
        using var txn = _db.Database.BeginTransaction();

        var seedSuffix = $"[SEED:{label}]";
        var seedUserPrefix = $"seed-{label}-";

        var orgs = _db.Organizations.Where(o => o.Name.EndsWith(seedSuffix)).ToList();
        var orgIds = orgs.Select(o => o.Id).ToList();

        if (orgIds.Count > 0)
        {
            var orgUsers = _db.OrganizationUsers.Where(ou => orgIds.Contains(ou.OrganizationId)).ToList();
            _db.OrganizationUsers.RemoveRange(orgUsers);
        }

        _db.Organizations.RemoveRange(orgs);

        var users = _db.Users.Where(u => u.Email.StartsWith(seedUserPrefix)).ToList();
        _db.Users.RemoveRange(users);

        usersDeleted = users.Count;
        orgsDeleted = orgs.Count;

        _db.SaveChanges();
        txn.Commit();
    }
}


