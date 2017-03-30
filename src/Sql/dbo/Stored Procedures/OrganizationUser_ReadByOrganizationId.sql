CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND (@Type IS NULL OR [Type] = @Type)
END