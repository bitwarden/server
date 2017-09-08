IF OBJECT_ID('[dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByFreeOrganizationAdminUser]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[Organization] O ON O.Id = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Type] < 2 -- Owner or Admin
        AND O.[PlanType] = 0 -- Free
        AND OU.[Status] = 2 -- 2 = Confirmed
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOrganizationOwnerUser]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUser] OU
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Type] = 0
        AND OU.[Status] = 2 -- 2 = Confirmed
END
GO
