CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION User_DeleteById

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] = @Id

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
