-- Item Sends are no longer deleted along with their cipher. The foreign key would block the cipher delete,
-- so it is dropped and item Sends whose cipher is gone are denied on access and removed by a background job.
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE object_id = OBJECT_ID('[dbo].[FK_Send_Cipher]')
)
BEGIN
    ALTER TABLE [dbo].[Send]
        DROP CONSTRAINT [FK_Send_Cipher]
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('[dbo].[Send]')
        AND name = 'IX_Send_CipherId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Send_CipherId]
        ON [dbo].[Send] ([CipherId] ASC)
END
GO

-- Item Sends are now looked up through CipherId, so the LIKE-based lookup procedure is no longer used.
DROP PROCEDURE IF EXISTS [dbo].[Send_ReadByItemIds]
GO

CREATE OR ALTER PROCEDURE [dbo].[Send_ReadItemSendsWithDeletedCipher]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SendView] S
    WHERE
        S.[Type] = 2
        AND S.[CipherId] IS NOT NULL
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[Cipher] C
            WHERE
                C.[Id] = S.[CipherId]
        )
END
GO

-- Backfill CipherId for existing item Sends from the item id in their metadata. The server writes item data
-- with PascalCase property names, so only that path is read. The cipher is not required to exist: a Send whose
-- cipher is already gone is linked so the background job can remove it.
UPDATE
    [dbo].[Send]
SET
    [CipherId] = TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE([Data], '$.Metadata.ItemId'))
WHERE
    [Type] = 2
    AND [CipherId] IS NULL
    AND ISJSON([Data]) = 1
GO
