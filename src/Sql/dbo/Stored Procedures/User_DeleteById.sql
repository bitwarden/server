CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION User_DeleteById

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT
    END

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
