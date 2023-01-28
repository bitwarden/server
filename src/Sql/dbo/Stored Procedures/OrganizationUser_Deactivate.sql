CREATE PROCEDURE [dbo].[OrganizationUser_Deactivate]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = -1 -- Deactivated
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
