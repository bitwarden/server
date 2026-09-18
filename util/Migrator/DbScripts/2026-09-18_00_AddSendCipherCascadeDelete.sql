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
CREATE PROCEDURE [dbo].[Send_ReadByCipherIds]
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
