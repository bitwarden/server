IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersById]
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
GO
