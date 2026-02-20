-- Phase 2 (Transition): Populate FileId for existing OrganizationReport records
-- This runs after deployment, performs batched data migration

-- Set FileId to 'Legacy' for all existing v1 reports (data stored in DB, not blob storage)
-- This sentinel value makes it easy to distinguish v1 (DB-stored) from v2 (blob-stored) reports
UPDATE [dbo].[OrganizationReport]
SET [FileId] = 'Legacy'
WHERE [FileId] IS NULL;
GO
