CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    BEGIN TRANSACTION User_DeleteById

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id
            AND [Type] > 0

        SET @BatchSize = @@ROWCOUNT
    END

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] = @Id
        AND [Type] = 0

    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
