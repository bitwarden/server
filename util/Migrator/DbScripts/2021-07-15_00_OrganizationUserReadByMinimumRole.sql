IF OBJECT_ID('[dbo].[OrganizationUser_ReadByMinimumRole]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadByMinimumRole]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadByMinimumRole]
    @OrganizationId UNIQUEIDENTIFIER,
    @MinRole TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        OrganizationId = @OrganizationId 
        AND [Type] <= @MinRole
END
