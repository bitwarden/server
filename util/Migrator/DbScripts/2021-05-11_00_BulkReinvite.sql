IF OBJECT_ID('[dbo].[OrganizationUser_ReadByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadByIds]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
    BEGIN
        RETURN(-1)
    END

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
END
