IF OBJECT_ID('[dbo].[OrganizationUser_ReadByUserIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadByUserIds]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadByUserIds]
    @UserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @UserIds) < 1
    BEGIN
        RETURN(-1)
    END

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [UserId] IN (SELECT [Id] FROM @UserIds)
END
GO
