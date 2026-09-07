-- Adds AccessLease_ReadActiveByCipherId: any member's in-window lease on a cipher, for the pre-check.
-- Cipher-scoped like the singleton guard; the index gains [NotAfter] DESC to seek.
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
