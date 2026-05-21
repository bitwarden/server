**Overall Assessment:** APPROVE

This PR renames repository calls in `CleanUpOrganizationEventsJob` to align with the new `IOrganizationEventCleanupRepository` interface (`ClaimNextPendingAsync`, `UpdateProgressAsync`, `UpdateCompletedAsync`, `UpdateErrorAsync`) and removes the explicit `MarkStartedAsync` call now that claiming is atomic. The accompanying unit tests are updated in lockstep, and the control flow (cancellation, error path, paused vs completed) is preserved.

<details>
<summary>Code Review Details</summary>

No findings. Notes for context only:

- The 4-minute `_runBudget` keeps a single job run safely inside the new 10-minute lease window, and `UpdateProgressAsync` is called after every non-empty batch, so the lease heartbeat requirement is satisfied for the expected workload.
- The pre-existing behavior where an immediately-cancelled run would still reach the `deleted == 0` completion branch is unchanged by this PR and out of scope here.

</details>
