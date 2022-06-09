CREATE PROCEDURE [dbo].[OrganizationUser_Disable]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = -1 -- Disabled
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
