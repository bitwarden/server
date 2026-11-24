-- Drops the approver lease history read. No client reads GET /leases/history, and it returned every ended lease
-- in the window with no page size.

DROP PROCEDURE IF EXISTS [dbo].[AccessLease_ReadManyEndedByCollectionIds]
GO
