using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB.EntityFrameworkCore;
using AutoMapper;
using EfCohortAssignment = Bit.Infrastructure.EntityFramework.Billing.Models.OrganizationPlanMigrationCohortAssignment;
using EfOrganization = Bit.Infrastructure.EntityFramework.AdminConsole.Models.Organization;
using EfPlayItem = Bit.Infrastructure.EntityFramework.Models.PlayItem;
using CoreCohort = Bit.Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohort;
using CoreCohortAssignment = Bit.Core.Billing.Organizations.PlanMigration.Entities.OrganizationPlanMigrationCohortAssignment;
using CoreOrganization = Bit.Core.AdminConsole.Entities.Organization;
using MigrationPath = Bit.Core.Billing.Organizations.PlanMigration.Enums.MigrationPathId;

namespace Bit.Seeder.Scenes;

public readonly struct MigrationCohortExportSceneResult
{
    public Guid CohortId { get; init; }
    public bool CohortCreated { get; init; }
    public int OrganizationsCreated { get; init; }
    public int AssignmentsCreated { get; init; }
}

/// <summary>
/// Seeds a migration cohort plus N inert organizations, each assigned to the cohort, so the
/// Admin Portal cohort-management table has a populated cohort ready to "Export CSV".
/// </summary>
/// <remarks>
/// The HTTP/API equivalent of the PM-36965 SQL seed script. The organizations are throwaway
/// placeholders (Disabled, no billing, no users) and exist only because
/// <c>OrganizationPlanMigrationCohortAssignment</c> has a FK to <c>Organization</c> with a UNIQUE
/// constraint on <c>OrganizationId</c> -- every assignment needs its own real organization row.
///
/// Organizations and assignments are bulk-inserted via LinqToDB <c>BulkCopy</c> (the same path
/// <see cref="Pipeline.BulkCommitter"/> uses) so counts in the tens of thousands stay fast. Each
/// organization's <c>CreationDate</c> is spread across distinct seconds. The export, however, orders
/// on the <em>assignment's</em> <c>(CreationDate, Id)</c>, and the assignment's <c>CreationDate</c>
/// has an internal setter unreachable from this assembly, so every assignment shares a near-identical
/// <c>DateTime.UtcNow</c> and the cursor advances on the <c>Id</c> tiebreaker. That is exactly the
/// bulk-loaded case the export's stored procedure is documented to handle, so it remains correct.
///
/// Cleanup: <c>BulkCopy</c> bypasses the repository <c>CreateAsync</c> hook that normally records
/// play-id tracking rows, so this scene bulk-inserts the <see cref="PlayItem"/> rows itself when the
/// request carries an <c>x-play-id</c>. That lets <c>DELETE /seed/{playId}</c> tear the seeded
/// organizations down (their assignments cascade away via the FK). The cohort row is NOT play-id
/// tracked -- <c>DestroySceneCommand</c> only deletes users/organizations -- so an emptied cohort is
/// left behind after a play-id delete; remove it by name if desired
/// (<c>DELETE FROM dbo.OrganizationPlanMigrationCohort WHERE Name = '&lt;CohortName&gt;'</c>).
///
/// LOCAL / NON-PROD ONLY -- the SeederApi refuses to run in production.
/// </remarks>
public class MigrationCohortExportScene(
    DatabaseContext db,
    IMapper mapper,
    IPlayIdService playIdService,
    IOrganizationPlanMigrationCohortRepository cohortRepository)
    : IScene<MigrationCohortExportScene.Request, MigrationCohortExportSceneResult>
{
    /// <summary>
    /// Inert plan applied to seeded placeholder organizations. Arbitrary -- these orgs never
    /// behave like live organizations (Enabled = false, no billing, no users).
    /// </summary>
    private const PlanType InertPlanType = PlanType.EnterpriseAnnually;

    /// <summary>Anchor for the spread CreationDate, matching the SQL seed script's baseline.</summary>
    private static readonly DateTime _creationAnchor = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public class Request
    {
        [Required]
        public required string CohortName { get; set; }

        [Required]
        [Range(1, 100_000)]
        public required int OrgCount { get; set; }

        /// <summary>
        /// Migration path for the cohort. Null produces a Churn-only cohort. Defaults to
        /// <see cref="MigrationPath.Enterprise2020AnnualToCurrent"/> to mirror the SQL seed script.
        /// </summary>
        public MigrationPath? MigrationPathId { get; set; } = MigrationPath.Enterprise2020AnnualToCurrent;

        /// <summary>
        /// Distinctive name prefix for the seeded organizations so they can be identified and
        /// cleaned up. Must stay short enough that the suffixed name fits Organization.Name.
        /// </summary>
        [MaxLength(40)]
        public string NamePrefix { get; set; } = "pm36965-seed-";
    }

    public async Task<SceneResult<MigrationCohortExportSceneResult>> SeedAsync(Request request)
    {
        var (cohort, cohortCreated) = await GetOrCreateCohortAsync(request);

        var organizations = BuildOrganizations(request);
        var assignments = BuildAssignments(organizations, cohort.Id);

        // BulkCopy maps Core entities to their EF counterparts (navigation properties are ignored).
        db.BulkCopy(organizations.Select(mapper.Map<EfOrganization>));
        db.BulkCopy(assignments.Select(mapper.Map<EfCohortAssignment>));

        // Record play-id tracking rows for the organizations so DELETE /seed/{playId} can clean them
        // up. BulkCopy bypasses the repository CreateAsync hook that normally does this, so the scene
        // inserts the PlayItem rows directly -- and in bulk, to keep the fast path fast.
        RecordOrganizationsForPlayId(organizations);

        return new SceneResult<MigrationCohortExportSceneResult>(
            result: new MigrationCohortExportSceneResult
            {
                CohortId = cohort.Id,
                CohortCreated = cohortCreated,
                OrganizationsCreated = organizations.Count,
                AssignmentsCreated = assignments.Count,
            },
            mangleMap: []);
    }

    private async Task<(CoreCohort Cohort, bool Created)> GetOrCreateCohortAsync(Request request)
    {
        var existing = await cohortRepository.GetByNameAsync(request.CohortName);
        if (existing is not null)
        {
            return (existing, false);
        }

        var cohort = new CoreCohort
        {
            Name = request.CohortName,
            MigrationPathId = request.MigrationPathId,
            IsActive = true,
        };
        cohort.SetNewId();

        var created = await cohortRepository.CreateAsync(cohort);
        return (created, true);
    }

    private static List<CoreOrganization> BuildOrganizations(Request request)
    {
        var organizations = new List<CoreOrganization>(request.OrgCount);

        for (var n = 1; n <= request.OrgCount; n++)
        {
            // Spread CreationDate across distinct seconds so the export keyset cursor advances over
            // real dates, exactly as the SQL seed script does.
            var creationDate = _creationAnchor.AddSeconds(n);
            var suffix = n.ToString("D6", CultureInfo.InvariantCulture);

            organizations.Add(new CoreOrganization
            {
                Id = CoreHelpers.GenerateComb(),
                Name = $"{request.NamePrefix}{suffix}",
                BillingEmail = $"{request.NamePrefix}{n}@example.com",
                Plan = "Enterprise (Annually)",
                PlanType = InertPlanType,
                Status = OrganizationStatusType.Created,
                Enabled = false, // never behaves like a live organization
                CreationDate = creationDate,
                RevisionDate = creationDate,
            });
        }

        return organizations;
    }

    private static List<CoreCohortAssignment> BuildAssignments(
        IEnumerable<CoreOrganization> organizations, Guid cohortId)
    {
        var assignments = new List<CoreCohortAssignment>();

        foreach (var org in organizations)
        {
            // CreationDate has an internal setter and defaults to DateTime.UtcNow at construction;
            // it cannot be set from this assembly. RevisionDate is settable, so align it for tidiness.
            var assignment = new CoreCohortAssignment
            {
                OrganizationId = org.Id,
                CohortId = cohortId,
            };
            assignment.RevisionDate = assignment.CreationDate;
            assignment.SetNewId();
            assignments.Add(assignment);
        }

        return assignments;
    }

    /// <summary>
    /// Bulk-inserts a <see cref="PlayItem"/> row per organization when the request is part of a play
    /// session, mirroring what the repository tracking decorators do on <c>CreateAsync</c>. No-op when
    /// no <c>x-play-id</c> is set. Deleting an organization cascade-removes its <see cref="PlayItem"/>
    /// row, so <c>DELETE /seed/{playId}</c> stays self-consistent.
    /// </summary>
    private void RecordOrganizationsForPlayId(IEnumerable<CoreOrganization> organizations)
    {
        if (!playIdService.InPlay(out var playId))
        {
            return;
        }

        var playItems = organizations.Select(org =>
        {
            var playItem = PlayItem.Create(org, playId);
            playItem.SetNewId(); // repository normally assigns the COMB id; BulkCopy bypasses it
            return playItem;
        });
        db.BulkCopy(playItems.Select(mapper.Map<EfPlayItem>));
    }
}
