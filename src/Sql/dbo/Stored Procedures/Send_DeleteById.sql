CREATE PROCEDURE [dbo].[Send_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM [dbo].[Folder] WHERE [Id] = @Id)

    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END