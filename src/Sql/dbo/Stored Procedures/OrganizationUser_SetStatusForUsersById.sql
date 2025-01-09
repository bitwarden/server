CREATE PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersById]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
    SET OU.[Status] = @Status

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END

