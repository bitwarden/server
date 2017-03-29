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
        AND [Email] = @Email
END