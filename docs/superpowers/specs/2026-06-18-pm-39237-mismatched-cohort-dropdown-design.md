# PM-39237 — Fix plan-mismatched cohort dropdown (silent unassign)

**Type:** Bug fix | **Priority:** High | **Severity:** Medium | **Team:** Billing
**Jira:** https://bitwarden.atlassian.net/browse/PM-39237
**Branch:** `billing/pm-39237/fix-mismatched-cohort-dropdown`

## Problem

An organization can be bulk-assigned (via PM-36963) to a migration cohort whose
`MigrationPath.FromPlan` does not match the org's current `PlanType` — plan-mismatch
detection in the bulk path is *informational and never blocks*. The row is written
correctly to `OrganizationPlanMigrationCohortAssignment`.

However, the single-org Admin Portal Edit page was never updated to tolerate such an
assignment. Two defects result:

1. **Display:** The "Migration cohort" dropdown shows **"(Not assigned)"** even though
   an assignment exists in the DB.
2. **Silent unassign:** Saving the form (without the operator touching the dropdown)
   **deletes** the assignment with no warning.

### Root cause

In `src/Admin/AdminConsole/Controllers/OrganizationsController.cs`:

- **GET (~lines 246–288):** `visibleCohorts` is built by keeping only churn-only cohorts
  (`!c.MigrationPathId.HasValue`) or cohorts whose
  `MigrationPaths.FromId(c.MigrationPathId.Value)?.FromPlan == organization.PlanType`.
  A plan-mismatched assigned cohort is filtered out. `MigrationCohortId` is still set to
  that excluded cohort's id, so in `_OrganizationForm.cshtml` (~lines 69–101) the
  `Selected = Model.MigrationCohortId == c.Id` comparison never matches → renders
  "(Not assigned)".
- **POST (`ResolveMigrationCohortAssignmentChangeAsync`, ~lines 664–710):** Because the
  dropdown rendered as unselected, an unchanged save posts `MigrationCohortId = null`.
  The resolver sees `submittedCohortId (null) != assignmentToReplace.CohortId`, treats it
  as a change, and deletes the assignment (write path ~lines 376–392) with no error.

The hidden round-trip input that protects the value only exists when the dropdown is
*disabled* (locked), so an editable (non-locked) mismatched assignment is unprotected.

## Decisions

1. **Mismatch behavior: Show + allow keep, block re-pick.**
   Render the org's actual mismatched cohort in the dropdown (labeled as a plan
   mismatch) so it round-trips and a no-touch save does not unassign it. Keep the
   existing guard that blocks assigning a *new/different* mismatched cohort via the
   single-org path. Display tolerates mismatch; editing stays plan-constrained.

2. **Save semantics when switching away from a mismatch: Allow the change normally.**
   Switching a mismatched assignment to (Not assigned) or to a plan-matching cohort is a
   real, intentional edit — processed as a normal delete/replace. Only a no-touch save
   (mismatch still selected) keeps the row. No confirmation prompt — heavier than the
   defect requires.

## Design

Three coordinated changes, all within the existing Admin edit path. No new permission
(governed by the existing `CanManagePlanMigrationCohortAssignment()` /
`Tools_ManagePlanMigrationCohorts`). No schema or repository signature changes.

### 1. GET — include the current cohort even when mismatched

In `OrganizationsController.cs` Edit GET, after building `visibleCohorts`:

- If `currentAssignment?.CohortId` is set and that cohort is **not** already in
  `visibleCohorts`, fetch it (`GetByIdAsync`) and union it into the list so the dropdown
  can render and round-trip the selected value.
- Surface a signal the view can use to label it — e.g. a `MigrationCohortMismatch`
  boolean on `OrganizationEditModel`, computed as "current assigned cohort has a
  `MigrationPathId` whose `FromPlan != organization.PlanType`".

### 2. View — label the mismatched option

In `_OrganizationForm.cshtml` (~lines 69–101), when the selected option is plan-mismatched,
append a `(plan mismatch)` suffix to its display text, consistent with the existing
`(inactive)` labeling. The `Selected` comparison now matches because the cohort is present
in the list.

### 3. POST — keep-on-no-change, block new mismatch

In `ResolveMigrationCohortAssignmentChangeAsync`:

- Re-submitting the same (mismatched) id → `submittedCohortId == assignmentToReplace.CohortId`
  → already returns `NoChange`. With GET now rendering the mismatch as selected, the
  default save posts the real id and the row is left untouched. **This is the fix for the
  silent unassign.**
- Switching to (Not assigned) → delete (intended).
- Switching to a plan-matching cohort → replace (intended).
- Switching to a *different* plan-mismatched cohort → blocked by the existing
  compatibility guard ("The selected migration cohort is not compatible with this
  organization's plan.").
- Locked assignments retain existing read-only/disabled behavior and lock messages.

## Error handling

No new error surfaces. Existing guard messages and lock-reason messages are unchanged.

## Testing (xUnit, `SutProvider` / `BitAutoData`, with mocking)

Controller-level tests around the Edit GET/POST and `ResolveMigrationCohortAssignmentChangeAsync`:

1. **GET, mismatched assignment** → assigned cohort appears in
   `AvailableMigrationCohorts` and `MigrationCohortId` is set to it;
   `MigrationCohortMismatch` is true.
2. **GET, matching assignment** → behaves as today; `MigrationCohortMismatch` false.
3. **POST no-change (regression)** → mismatched id re-submitted → `NoChange`, repository
   `DeleteAsync`/`CreateAsync` never called.
4. **POST clear** → mismatch → null → `DeleteAsync` called, no `CreateAsync`.
5. **POST switch to valid cohort** → delete old + create new.
6. **POST switch to a different mismatched cohort** → blocked with the compatibility error;
   no writes.
7. **POST locked mismatched assignment** → still blocked with the lock message.

## Scope / non-goals

- No change to the bulk-assign path (PM-36963) — its mismatch-allowing semantics are intended.
- No new permission, schema, repository signature, or EF/Dapper changes.
- No confirmation-prompt UI.
- No unrelated refactoring of the Admin organizations controller.

## Key references

- Filter logic: `src/Admin/AdminConsole/Controllers/OrganizationsController.cs:246–288`
- Save resolver: `OrganizationsController.cs:664–710`; write/delete: `:376–392`
- View dropdown: `src/Admin/AdminConsole/Views/Shared/_OrganizationForm.cshtml:69–101`
- View model: `src/Admin/AdminConsole/Models/OrganizationEditModel.cs:216–227`
- Entities/repos: `src/Core/Billing/Organizations/PlanMigration/`
  (`OrganizationPlanMigrationCohortAssignment.IsLocked()`, `MigrationPaths.FromId(...)`,
  `GetByOrganizationIdAsync` / `GetByIdAsync` / `CreateAsync` / `DeleteAsync`)
