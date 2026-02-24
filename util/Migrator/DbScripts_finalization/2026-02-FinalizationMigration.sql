-- Remove all DisableSend (type 6) policy rows.
-- These were consolidated into SendOptions (type 7) rows with disableSend=true
-- in the Phase 1 migration (2026-02-24_00_ConsolidateDisableSendIntoSendOptions.sql).
-- This finalization runs during the next release deployment once no rollback to the
-- previous release is possible, making the removal of type 6 rows safe.

-- Move this file to DbScripts/ as part of the next release.

DELETE FROM [dbo].[Policy]
WHERE [Type] = 6;
GO
