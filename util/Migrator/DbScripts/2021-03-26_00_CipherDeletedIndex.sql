IF OBJECT_ID('[dbo].[Cipher_DeleteDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteDeleted]
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteDeleted]
    @DeletedDateBefore DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [DeletedDate] < @DeletedDateBefore

        SET @BatchSize = @@ROWCOUNT
    END
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Cipher_DeletedDate'
    AND object_id = OBJECT_ID('[dbo].[Cipher]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Cipher_DeletedDate]
        ON [dbo].[Cipher]([DeletedDate] ASC)
END
GO
