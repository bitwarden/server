-- PAM Credential Leasing: supporting index for the governance lease lists. AccessLease_ReadManyActiveByCollectionIds
-- and AccessLease_ReadManyEndedByCollectionIds (GET /leases/active, /leases/history) filter AccessLease by the
-- caller's manageable collection ids; without a CollectionId index those scans fall back to the table. This adds the
-- seekable index. No behaviour change.
--
-- Feature is behind the pm-37044-pam-v-0 flag (unshipped POC); server + migration deploy together.

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [Name] = 'IX_AccessLease_CollectionId_Status' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_CollectionId_Status]
        ON [dbo].[AccessLease] ([CollectionId] ASC, [Status] ASC);
END
GO
