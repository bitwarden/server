CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdEmail]
    @OrganizationId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256)
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