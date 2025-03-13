CREATE PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersByGuidIdArray]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE OU
    SET OU.[Status] = @Status
    FROM [dbo].[OrganizationUser] OU
    INNER JOIN @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END
