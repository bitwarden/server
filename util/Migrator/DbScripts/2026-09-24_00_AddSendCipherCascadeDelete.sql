-- Add a supporting index for FK_Send_Cipher before the cascade is enabled, so that neither the
-- cascading delete nor Send_ReadByCipherIds has to scan [dbo].[Send]
IF NOT EXISTS (
    SELECT *
    FROM sys.indexes
    WHERE name = 'IX_Send_CipherId'
        AND object_id = OBJECT_ID('[dbo].[Send]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Send_CipherId]
        ON [dbo].[Send] ([CipherId] ASC)
END
GO

-- Update FK_Send_Cipher to cascade delete
IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'FK_Send_Cipher'
        AND TABLE_NAME = 'Send'
)
BEGIN
    ALTER TABLE [dbo].[Send]
        DROP CONSTRAINT [FK_Send_Cipher]
END
GO

ALTER TABLE [dbo].[Send]
    ADD CONSTRAINT [FK_Send_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE
GO

-- Create new stored procedure for reading Sends by Cipher IDs
CREATE OR ALTER PROCEDURE [dbo].[Send_ReadByCipherIds]
    @CipherIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [CipherId] IN (SELECT * FROM @CipherIds)
END
GO
