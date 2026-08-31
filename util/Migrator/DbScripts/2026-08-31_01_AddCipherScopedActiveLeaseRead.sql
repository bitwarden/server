-- Add AccessLease_ReadActiveByCipherId: whether ANY member currently holds an in-window lease on a cipher, and when
-- it ends. PM-42446's access pre-check needs this so the request form can tell a requester the single-active-lease
-- slot is taken, instead of promising immediate access and failing only at "Start access".
--
-- Cipher-scoped on purpose. The singleton guard in [AccessLease_CreateFromApprovedRequest] filters on CipherId alone
-- with no collection predicate, so reusing the collection-scoped [AccessLease_ReadManyActiveByCollectionIds] over the
-- caller's reachable collections would miss a holder who reaches the cipher through a collection the caller cannot
-- see.
--
-- [IX_AccessLease_CipherId_Action] gains [NotAfter] DESC as a third key column so this read seeks to the in-window
-- rows in the order it returns them. Its two leading columns are unchanged, so the singleton guard that shares the
-- index is unaffected. Without the third column the read looks up every Action = 0 row for the cipher -- the whole
-- happy-path lease history, which never stops growing -- and then sorts.

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AccessLease_CipherId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]')
)
    AND NOT EXISTS (
        SELECT 1
        FROM sys.index_columns IC
        INNER JOIN sys.indexes I ON I.[object_id] = IC.[object_id] AND I.[index_id] = IC.[index_id]
        INNER JOIN sys.columns C ON C.[object_id] = IC.[object_id] AND C.[column_id] = IC.[column_id]
        WHERE I.[name] = 'IX_AccessLease_CipherId_Action'
            AND I.[object_id] = OBJECT_ID('[dbo].[AccessLease]')
            AND C.[name] = 'NotAfter'
    )
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_CipherId_Action]
        ON [dbo].[AccessLease] ([CipherId] ASC, [Action] ASC, [NotAfter] DESC)
        WITH (DROP_EXISTING = ON);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadActiveByCipherId]
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [CipherId] = @CipherId
        AND [Action] = 0 -- None (no early end)
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] DESC
END
GO
