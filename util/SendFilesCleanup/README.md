# SendFilesCleanup

A one-off console utility to identify and delete orphaned blobs in the `sendfiles` Azure Storage container — blobs whose parent `Send` row has been deleted from the database.

See [PR #7386](https://github.com/bitwarden/server/pull/7386) for the prevention work that stops new orphans from being created. This tool cleans up historical orphans that existed before that PR was deployed.

## Usage

```
dotnet run --project util/SendFilesCleanup -- \
  --sql-connection "<mssql read-only connection string>" \
  --blob-connection "<azure storage connection string>" \
  [--container sendfiles] \
  [--output-dir ./output] \
  [--min-age-hours 24] \
  [--execute]
```

- **Dry-run is the default.** Without `--execute`, the tool produces reports only — no blobs are deleted.
- `--min-age-hours` excludes blobs modified within the last N hours (default `24`). This buffers against in-flight uploads whose DB rows may not yet be committed.
- `--container` defaults to `sendfiles` (the Send blob container name from `AzureSendFileStorageService.FilesContainerName`).
- `--output-dir` defaults to `./output` and is created if missing.

## Output files

Produced in `--output-dir`:

| File | Purpose |
|---|---|
| `all_blobs.csv` | Snapshot of every blob in the container (`Name,LastModified,ContentLength`). |
| `valid_blobs.txt` | The whitelist: blob paths derived from rows in `dbo.Send` where `Type = 1` and `JSON_VALUE(Data, '$.Id') IS NOT NULL`. |
| `orphans.txt` | Blobs classified as orphans (not in whitelist, older than cutoff, not already deleted). |
| `orphans_summary.txt` | Count, total bytes, oldest/newest timestamps, cutoff, and input sizes. |
| `deleted.log` | Append-only log of successfully deleted (or already-gone) blob paths. Re-runs skip anything in this file. |
| `errors.log` | Append-only log of blobs whose batch delete returned a non-success status. |

## Safety

1. **Dry run first.** Review `orphans.txt` and `orphans_summary.txt`. Spot-check a few paths against the DB:
   ```sql
   SELECT Id FROM [dbo].[Send] WHERE Id = '<sendId from orphan path>';
   ```
   Should return zero rows.
2. **Enable soft delete** on the `sendfiles` container before running with `--execute`. If an orphan is misidentified, the soft-delete retention window allows recovery.
3. **Run after PR #7386 is deployed** to the target region. Otherwise new orphans will accumulate between the snapshot and the deploy.
4. **Resumability.** If the tool is interrupted during deletion, re-running in `--execute` mode will skip paths already in `deleted.log`.

## How it works

1. **List blobs first.** The container is enumerated via `BlobContainerClient.GetBlobsAsync`. This happens *before* the DB query so that any `Send` uploaded mid-run is guaranteed to already exist in the whitelist by the time it is read.
2. **Load whitelist.** Direct Dapper query against `dbo.Send` (no EF, no Core project reference):
   ```sql
   SELECT LOWER(CAST(s.Id AS NVARCHAR(36))) + '/' + JSON_VALUE(s.Data, '$.Id') AS BlobPath
   FROM [dbo].[Send] s
   WHERE s.[Type] = 1
     AND JSON_VALUE(s.Data, '$.Id') IS NOT NULL
   ```
   `Send.Data` is not DataProtector-encrypted (only `Send.Emails` is), so `JSON_VALUE` works directly.
3. **Compute set difference.** Orphan = blob not in whitelist AND `LastModified < now - minAgeHours` AND not already in `deleted.log`.
4. **Delete in batches of 256** via `BlobBatchClient.SubmitBatchAsync(throwOnAnyFailure: false)`. Per-blob responses are inspected: HTTP 202 / 404 are recorded in `deleted.log`; anything else lands in `errors.log` with its status code.

## Exit codes

- `0` — Success (dry run completed, or execute completed with zero failures).
- `1` — Deletion ran but at least one blob failed.
- `2` — Missing required argument.
