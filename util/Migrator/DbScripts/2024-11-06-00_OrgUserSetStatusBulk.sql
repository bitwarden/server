CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersById]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON
    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = @Status
    WHERE
        [Id] IN (SELECT Id from @OrganizationUserIds)
END

EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
