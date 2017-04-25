CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId]
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END