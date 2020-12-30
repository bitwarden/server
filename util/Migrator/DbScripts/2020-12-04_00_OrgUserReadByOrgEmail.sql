IF OBJECT_ID('[dbo].[OrganizationUser_ReadByOrganizationIdEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdEmail]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdEmail]
    @OrganizationId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Email] IS NOT NULL
        AND @Email IS NOT NULL
        AND [Email] = @Email
END
GO
