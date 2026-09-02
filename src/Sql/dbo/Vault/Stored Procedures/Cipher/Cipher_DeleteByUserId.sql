CREATE PROCEDURE [dbo].[Cipher_DeleteByUserId]
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Cipher_DeleteByUserId_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @UserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByUserId_Ciphers
    END

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @UserId

    -- Cleanup user
    EXEC [dbo].[User_UpdateStorage] @UserId
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END