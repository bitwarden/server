# Baseline

Fixture: `scale.md-balanced-sterling-cooper --mangle` (MSSQL); ground truth via `capture-ground-truth.sh`. One run per case, variance not measured.

## Results (assertions passed / total)

| Eval                                | Pass rate |
| ----------------------------------- | --------- |
| 0 active-member-count               | 2/2       |
| 1 member-status-breakdown           | 2/2       |
| 2 user-visible-ciphers              | 3/3       |
| 3 top-users-direct-collection       | 3/3       |
| 4 active-vs-deleted-ciphers         | 2/2       |
| 5 archived-org-ciphers              | 2/2       |
| 6 personal-ciphers-of-members       | 2/2       |
| 7 org-plan-and-active-state         | 2/2       |
| 8 encrypted-column-trap             | 2/2       |
| 9 org-abilities-via-view            | 2/2       |
| 13 collection-permission-resolution | 2/2       |
| **Mean per-eval pass rate**         | **1.00**  |
