CREATE PROCEDURE [dbo].[CollectionUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationUserId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationUserId] FROM [dbo].[CollectionUser] WHERE [Id] = @Id)

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [Id] = @Id

    IF @OrganizationUserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
    END
END